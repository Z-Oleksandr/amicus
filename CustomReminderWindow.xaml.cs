using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace AMICUS
{
    /// <summary>
    /// Window for creating custom reminders
    /// </summary>
    public partial class CustomReminderWindow : Window
    {
        // Public properties for results
        public string ReminderMessage { get; private set; } = "";
        public DateTime ScheduledTime { get; private set; }

        public CustomReminderWindow()
        {
            InitializeComponent();

            // Set default date to today and time to next hour
            ReminderDatePicker.SelectedDate = DateTime.Today;
            var nextHour = DateTime.Now.AddHours(1);
            HourTextBox.Text = nextHour.Hour.ToString("00");
            MinuteTextBox.Text = "00";
        }

        /// <summary>
        /// Handles message text changes - updates character count and validates
        /// </summary>
        private void MessageTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            int count = MessageTextBox.Text.Length;
            CharCountText.Text = $"{count}/69";
            ValidateInput();
        }

        /// <summary>
        /// Handles date or time changes - validates input
        /// </summary>
        private void DateOrTime_Changed(object sender, EventArgs e)
        {
            ValidateInput();
        }

        /// <summary>
        /// Validates all input and enables/disables save button
        /// </summary>
        private void ValidateInput()
        {
            bool isValid = true;

            // Check message is not empty
            if (string.IsNullOrWhiteSpace(MessageTextBox.Text))
            {
                isValid = false;
            }

            // Check date is selected
            if (!ReminderDatePicker.SelectedDate.HasValue)
            {
                isValid = false;
            }

            // Check time is valid
            if (!int.TryParse(HourTextBox.Text, out int hour) || hour < 0 || hour > 23)
            {
                isValid = false;
            }

            if (!int.TryParse(MinuteTextBox.Text, out int minute) || minute < 0 || minute > 59)
            {
                isValid = false;
            }

            // Check that scheduled time is in the future
            if (isValid)
            {
                var scheduledTime = ReminderDatePicker.SelectedDate!.Value.Date
                    .AddHours(hour)
                    .AddMinutes(minute);

                if (scheduledTime <= DateTime.Now)
                {
                    isValid = false;
                }
            }

            SaveButton.IsEnabled = isValid;
        }

        /// <summary>
        /// Handles save button click
        /// </summary>
        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            ReminderMessage = MessageTextBox.Text.Trim();

            int hour = int.Parse(HourTextBox.Text);
            int minute = int.Parse(MinuteTextBox.Text);

            ScheduledTime = ReminderDatePicker.SelectedDate!.Value.Date
                .AddHours(hour)
                .AddMinutes(minute);

            DialogResult = true;
            Close();
        }

        /// <summary>
        /// Handles cancel button click
        /// </summary>
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        /// <summary>
        /// Allow window dragging
        /// </summary>
        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);
            if (e.OriginalSource is not System.Windows.Controls.TextBox &&
                e.OriginalSource is not System.Windows.Controls.DatePicker)
            {
                DragMove();
            }
        }
    }
}
