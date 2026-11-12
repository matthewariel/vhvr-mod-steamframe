using System;

namespace VhvrSteamFrame
{
    /// <summary>
    /// Where the Button guide page (page II of the Steam Frame tab) puts things, in the tab's units (x right, y up, 0 = the
    /// panel's centre). Plain numbers so a test can check that everything stays inside the panel.
    ///
    ///   left column of boxes  |   both controllers from above, handles down    |  right column of boxes
    ///   x = -440..-250        |   (frame_controllers.png, x = -250..250,       |  x = 250..440
    ///                         |   leader lines end at ±250); two-button        |
    ///                         |   combinations between the handles; a small    |
    ///                         |   side view (bumper, trigger) under each one   |
    ///   Reset to default      |                                                |  status note
    ///
    /// Clicking a box swaps the picture for a picker of actions (3 columns) with Cancel under it.
    /// The leader lines are part of the picture: draw_frame_controllers.ps1 must use the same box positions.
    /// </summary>
    public static class FrameGuideLayout
    {
        // Room the page has: inside the panel's edges (about ±450 and -252, from headset screenshots) with a margin,
        // and below the status line and scope legend.
        public const float EdgeX = 440f;
        public const float TopY = 165f;
        public const float BottomY = -240f;

        // Columns of boxes, one per controller, in FrameButtons.Left / Right order.
        public const float ColumnInnerX = 250f;
        public const float BoxWidth = EdgeX - ColumnInnerX;  // 190
        public const float BoxHeight = 33f;
        public const float BoxStep = 36.5f;
        public const float FirstBoxY = 147f;
        public const int BoxNameSize = 12;   // the button's name, first line
        public const int BoxActionSize = 13; // what it does, second line
        public const float ColumnCentreX = ColumnInnerX + BoxWidth / 2f; // 345

        public static float BoxY(int index) => FirstBoxY - BoxStep * index;

        // Under the columns: Reset to default (left) and the status note (right).
        public const float ResetY = -222f;
        public const float ResetHeight = 30f;
        public const float NoteHeight = 36f;
        public const int NoteSize = 12;

        // The picture, between the columns, all the way down.
        public const float PictureWidth = 2f * ColumnInnerX; // 500
        public const float PictureHeight = TopY - BottomY;   // 405
        public const float PictureCentreY = TopY - PictureHeight / 2f;

        // Two-button combinations, between the two handles, under the grip buttons on their necks (which end at y = -42).
        // draw_frame_controllers.ps1 -Preview uses the same numbers.
        public const float NotesWidth = 210f;
        public const float NotesTopY = -45f;
        public const int NotesSize = 13;

        // Picker: title, a grid of action buttons, Cancel, and a line for why a choice was refused.
        public const float PickerTitleY = 140f;
        public const int PickerColumns = 3;
        public const float PickerButtonWidth = 150f;
        public const float PickerButtonHeight = 32f;
        public const float PickerStepX = 158f;
        public const float PickerStepY = 38f;
        public const float PickerFirstY = 100f;
        public const float CancelY = -96f;
        public const float PickerMessageY = -140f;
        public const float PickerMessageWidth = 470f;
        public const float PickerMessageHeight = 40f;

        public static float PickerX(int index) => (index % PickerColumns - (PickerColumns - 1) / 2f) * PickerStepX;
        public static float PickerY(int index) => PickerFirstY - PickerStepY * (index / PickerColumns);
    }
}
