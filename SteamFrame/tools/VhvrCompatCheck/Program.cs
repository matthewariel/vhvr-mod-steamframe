using Mono.Cecil;
using Mono.Cecil.Cil;
using VhvrSteamFrame;

// Checks every VHVR / Valheim member the plugin depends on (src/VhvrSteamFrame/Core/VhvrApi.cs) against an installed game.
//
//   VhvrCompatCheck -ValheimDir "C:\...\Valheim"                 the check
//   VhvrCompatCheck -ValheimDir "..." -Describe FistCollision    list a type's members (to see what something is called now)
//
// Exit code 0 = everything the plugin needs is there; 1 = something required is missing or can't be patched; 2 = bad usage.

string valheimDir = @"C:\Program Files (x86)\Steam\steamapps\common\Valheim";
string describe = null;
for (int i = 0; i < args.Length; i++)
{
    if (args[i].Equals("-ValheimDir", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length) valheimDir = args[++i];
    else if (args[i].Equals("-Describe", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length) describe = args[++i];
    else { Console.Error.WriteLine("Unknown argument: " + args[i]); return 2; }
}

string managed = Path.Combine(valheimDir, "valheim_Data", "Managed");
string plugins = Path.Combine(valheimDir, "BepInEx", "plugins");
string vhvrPath = Path.Combine(plugins, "ValheimVRMod.dll");
string[] gameDlls = { "assembly_valheim.dll", "assembly_guiutils.dll", "assembly_utils.dll" };

if (!File.Exists(vhvrPath)) { Console.Error.WriteLine("VHVR not found: " + vhvrPath); return 2; }
foreach (var dll in gameDlls)
    if (!File.Exists(Path.Combine(managed, dll))) { Console.Error.WriteLine("Valheim DLL not found: " + Path.Combine(managed, dll)); return 2; }

var resolver = new DefaultAssemblyResolver();
resolver.AddSearchDirectory(managed);
resolver.AddSearchDirectory(plugins);
resolver.AddSearchDirectory(Path.Combine(valheimDir, "BepInEx", "core"));
var readerParameters = new ReaderParameters { AssemblyResolver = resolver };

var vhvr = AssemblyDefinition.ReadAssembly(vhvrPath, readerParameters);
var game = gameDlls.Select(d => AssemblyDefinition.ReadAssembly(Path.Combine(managed, d), readerParameters)).ToList();
var gameNames = new HashSet<string>(game.Select(a => a.Name.Name), StringComparer.OrdinalIgnoreCase);

if (describe != null) return Describe(describe);

string vhvrVersion = VhvrVersion();
bool tested = VhvrApi.TestedVhvrVersions.Contains(vhvrVersion);
Console.WriteLine($"VHVR {vhvrVersion ?? "(version not found)"} at {vhvrPath}");
Console.WriteLine(tested
    ? "  This version is in the plugin's tested list."
    : $"  Not in the plugin's tested list ({string.Join(", ", VhvrApi.TestedVhvrVersions)}). If everything below is OK and it plays fine, add it to VhvrApi.TestedVhvrVersions.");
Console.WriteLine();

int broken = 0, risky = 0, notes = 0;
foreach (var group in VhvrApi.All.GroupBy(e => e.Module))
{
    var problems = new List<string>();
    var warnings = new List<string>();
    foreach (var entry in group)
    {
        var found = Find(entry, out string why);
        if (found == null)
        {
            if (entry.Optional) { warnings.Add($"optional {entry} is missing ({why}): the module still starts"); notes++; }
            else { problems.Add($"BROKEN  {entry} is missing ({why})"); broken++; }
            continue;
        }

        // Harmony can't patch a VHVR method that touches non-public Valheim members.
        if (entry.Kind == ApiKind.HarmonyPatch && entry.Assembly == ApiAssembly.Vhvr)
        {
            foreach (var method in found.OfType<MethodDefinition>())
            {
                var hidden = NonPublicGameMembers(method);
                if (hidden.Count == 0) continue;
                string text = $"RISKY-PATCH  {entry} now uses non-public Valheim members ({string.Join(", ", hidden.Take(4))}): Harmony can't patch it";
                if (entry.Optional) { warnings.Add(text); notes++; }
                else { problems.Add(text); risky++; }
            }
        }
    }

    string status = problems.Count == 0 ? "OK" : problems.Any(p => p.StartsWith("BROKEN")) ? "BROKEN" : "RISKY-PATCH";
    Console.WriteLine($"{group.Key,-16} {status,-12} ({group.Count()} members checked)");
    foreach (var p in problems) Console.WriteLine("    " + p);
    foreach (var w in warnings) Console.WriteLine("    note: " + w);
}

Console.WriteLine();
Console.WriteLine("Known traps (VHVR methods the plugin deliberately does NOT patch):");
foreach (var trap in VhvrApi.KnownUnpatchable)
{
    var found = Find(trap, out string why);
    if (found == null) { Console.WriteLine($"    {trap}: gone ({why}). Check the workaround in the {trap.Module} module."); continue; }
    var hidden = found.OfType<MethodDefinition>().SelectMany(NonPublicGameMembers).Distinct().ToList();
    Console.WriteLine(hidden.Count > 0
        ? $"    {trap}: still unpatchable, as expected (uses {string.Join(", ", hidden.Take(3))})."
        : $"    {trap}: looks patchable now. The workaround in the {trap.Module} module could be simplified.");
}

Console.WriteLine();
if (broken == 0 && risky == 0)
{
    Console.WriteLine($"ALL OK: {VhvrApi.All.Count} members checked" + (notes > 0 ? $", {notes} note(s) above." : "."));
    return 0;
}
Console.WriteLine($"PROBLEMS: {broken} missing, {risky} unpatchable. The modules named above will switch themselves off with this VHVR/Valheim.");
return 1;

// ---------------------------------------------------------------------------------------------------------------------

TypeDefinition FindType(ApiEntry entry)
{
    var assemblies = entry.Assembly == ApiAssembly.Vhvr ? new List<AssemblyDefinition> { vhvr } : game;
    foreach (var assembly in assemblies)
        foreach (var module in assembly.Modules)
        {
            var type = module.GetType(entry.Type) ?? module.Types.FirstOrDefault(t => t.Name == entry.Type && string.IsNullOrEmpty(t.Namespace));
            if (type != null) return type;
        }
    return null;
}

// The type followed by its base types, as far as they can be resolved (the plugin's lookups also search base types).
IEnumerable<TypeDefinition> WithBases(TypeDefinition type)
{
    for (var t = type; t != null;)
    {
        yield return t;
        TypeDefinition next = null;
        try { next = t.BaseType?.Resolve(); } catch (AssemblyResolutionException) { }
        t = next;
    }
}

// The definitions an entry names (several for a method without a parameter list), or null with a reason.
List<IMemberDefinition> Find(ApiEntry entry, out string why)
{
    why = null;
    var type = FindType(entry);
    if (type == null) { why = "type " + entry.Type + " not found"; return null; }

    var result = new List<IMemberDefinition>();
    switch (entry.Shape)
    {
        case ApiShape.Type:
            result.Add(type);
            break;
        case ApiShape.Field:
            result.AddRange(WithBases(type).SelectMany(t => t.Fields).Where(f => f.Name == entry.Member).Take(1));
            break;
        case ApiShape.Getter:
            result.AddRange(WithBases(type).SelectMany(t => t.Properties).Where(p => p.Name == entry.Member && p.GetMethod != null).Select(p => p.GetMethod).Take(1));
            break;
        case ApiShape.Setter:
            result.AddRange(WithBases(type).SelectMany(t => t.Properties).Where(p => p.Name == entry.Member && p.SetMethod != null).Select(p => p.SetMethod).Take(1));
            break;
        case ApiShape.AnyMember:
            result.AddRange(WithBases(type).SelectMany(t => t.Fields.Cast<IMemberDefinition>().Concat(t.Properties).Concat(t.Methods))
                .Where(m => m.Name == entry.Member).Take(1));
            break;
        case ApiShape.Method:
            var methods = WithBases(type).SelectMany(t => t.Methods).Where(m => m.Name == entry.Member).ToList();
            if (entry.Parameters != null)
            {
                var match = methods.Where(m => m.Parameters.Select(p => p.ParameterType.FullName).SequenceEqual(entry.Parameters)).ToList();
                if (match.Count == 0 && methods.Count > 0)
                    why = "it exists, but with parameters " + string.Join(" | ", methods.Select(m => "(" + string.Join(", ", m.Parameters.Select(p => p.ParameterType.FullName)) + ")"));
                methods = match;
            }
            result.AddRange(methods);
            break;
    }

    if (result.Count > 0) return result;
    why = why ?? "not in " + type.Name;
    return null;
}

// Non-public members of Valheim's own DLLs that a method's code touches (resolved against the real, non-publicized DLLs).
List<string> NonPublicGameMembers(MethodDefinition method)
{
    var hidden = new List<string>();
    if (!method.HasBody) return hidden;
    foreach (var instruction in method.Body.Instructions)
    {
        if (!(instruction.Operand is MemberReference reference) || reference.DeclaringType == null) continue;
        string scope = (reference.DeclaringType.GetElementType().Scope as AssemblyNameReference)?.Name;
        if (scope == null || !gameNames.Contains(scope)) continue;

        try
        {
            string name = reference.DeclaringType.Name + "." + reference.Name;
            if (reference is FieldReference fieldRef)
            {
                var field = fieldRef.Resolve();
                if (field == null) hidden.Add(name + " (missing)");
                else if (!field.IsPublic || !IsVisible(field.DeclaringType)) hidden.Add(name);
            }
            else if (reference is MethodReference methodRef)
            {
                var target = methodRef.Resolve();
                if (target == null) hidden.Add(name + "() (missing)");
                else if (!target.IsPublic || !IsVisible(target.DeclaringType)) hidden.Add(name + "()");
            }
        }
        catch (AssemblyResolutionException) { /* not a game DLL after all */ }
    }
    return hidden.Distinct().ToList();
}

bool IsVisible(TypeDefinition type)
{
    for (var t = type; t != null; t = t.DeclaringType)
        if (!(t.IsPublic || t.IsNestedPublic)) return false;
    return true;
}

string VhvrVersion()
{
    foreach (var type in vhvr.MainModule.Types)
        foreach (var attribute in type.CustomAttributes)
            if (attribute.AttributeType.Name == "BepInPlugin" && attribute.ConstructorArguments.Count == 3)
                return attribute.ConstructorArguments[2].Value as string;
    return null;
}

int Describe(string name)
{
    var types = new[] { vhvr }.Concat(game).SelectMany(a => a.Modules).SelectMany(m => m.GetTypes())
        .Where(t => t.Name.Equals(name, StringComparison.OrdinalIgnoreCase) || t.FullName.Equals(name, StringComparison.OrdinalIgnoreCase)).ToList();
    if (types.Count == 0) { Console.WriteLine("No type named " + name + " in VHVR or Valheim."); return 1; }
    foreach (var type in types)
    {
        Console.WriteLine($"{type.FullName}  [{type.Module.Assembly.Name.Name}]" + (type.BaseType != null ? "  : " + type.BaseType.Name : ""));
        foreach (var f in type.Fields.OrderBy(f => f.Name))
            Console.WriteLine($"  field    {(f.IsPublic ? "public " : "private")} {(f.IsStatic ? "static " : "")}{f.FieldType.Name} {f.Name}");
        foreach (var p in type.Properties.OrderBy(p => p.Name))
            Console.WriteLine($"  property {p.PropertyType.Name} {p.Name} {{{(p.GetMethod != null ? " get;" : "")}{(p.SetMethod != null ? " set;" : "")} }}");
        foreach (var m in type.Methods.Where(m => !m.IsGetter && !m.IsSetter).OrderBy(m => m.Name))
            Console.WriteLine($"  method   {(m.IsPublic ? "public " : "private")} {(m.IsStatic ? "static " : "")}{m.ReturnType.Name} {m.Name}({string.Join(", ", m.Parameters.Select(p => p.ParameterType.Name + " " + p.Name))})");
        Console.WriteLine();
    }
    return 0;
}
