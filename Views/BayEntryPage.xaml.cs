using YardBayApp.Helpers;
using YardBayApp.Models;
using YardBayApp.Services;

namespace YardBayApp.Views
{
    public partial class BayEntryPage : ContentPage
    {
        private List<Bay> _bays = new();
        private IDispatcherTimer? _clockTimer;

        public BayEntryPage()
        {
            InitializeComponent();
            EntryDatePicker.Date = DateTime.Today;
            EntryTimePicker.Time = DateTime.Now.TimeOfDay;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();

            UpdateLiveClock();
            _clockTimer = Dispatcher.CreateTimer();
            _clockTimer.Interval = TimeSpan.FromSeconds(1);
            _clockTimer.Tick += (_, __) => UpdateLiveClock();
            _clockTimer.Start();

            try
            {
                _bays = await SupabaseService.Instance.GetBaysAsync();
            }
            catch (Exception ex)
            {
                StatusLabel.TextColor = Colors.Red;
                StatusLabel.Text = $"Could not load bays: {ex.Message}";
            }
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            _clockTimer?.Stop();
            _clockTimer = null;
        }

        /// <summary>Live Sri Lanka date/time shown at the top of the form - reference only,
        /// does NOT affect what gets saved (the Date/Time pickers below control that).</summary>
        private void UpdateLiveClock()
        {
            var nowSl = SriLankaTime.ToLocal(DateTime.UtcNow);
            LiveClockLabel.Text = nowSl.ToString("dddd, yyyy-MM-dd    HH:mm:ss");
        }

        private int ParseOrZero(string? text) =>
            int.TryParse(text, out var v) ? v : 0;

        private async void OnHistoryClicked(object sender, EventArgs e)
        {
            await Navigation.PushAsync(new HistoryPage());
        }

        private Guid BayIdFor(string code) =>
            _bays.FirstOrDefault(b => b.Code == code)?.Id ?? Guid.Empty;

        private async void OnSaveClicked(object sender, EventArgs e)
        {
            SaveButton.IsEnabled = false;
            StatusLabel.TextColor = Colors.Gray;
            StatusLabel.Text = "Saving...";

            try
            {
                // Supervisor picks date & time as Sri Lanka wall-clock time.
                // Convert to a real UTC value here, once, before it ever touches Supabase.
                var localRecordedAt = EntryDatePicker.Date.Date + EntryTimePicker.Time;
                var recordedAt = SriLankaTime.ToUtc(localRecordedAt);

                var inputs = new List<BayEntryInput>
                {
                    new() {
                        BayCode = "A", BayId = BayIdFor("A"),
                        Examine = ParseOrZero(AExamine.Text),
                        NotExamine = ParseOrZero(ANotExamine.Text),
                        Space40ft = ParseOrZero(ASpace40.Text),
                        Space20ft = ParseOrZero(ASpace20.Text)
                    },
                    new() {
                        BayCode = "B", BayId = BayIdFor("B"),
                        Examine = ParseOrZero(BExamine.Text),
                        NotExamine = ParseOrZero(BNotExamine.Text),
                        Space40ft = ParseOrZero(BSpace40.Text),
                        Space20ft = ParseOrZero(BSpace20.Text)
                    },
                    new() {
                        BayCode = "C", BayId = BayIdFor("C"),
                        Examine = ParseOrZero(CExamine.Text),
                        NotExamine = ParseOrZero(CNotExamine.Text),
                        Space40ft = ParseOrZero(CSpace40.Text),
                        Space20ft = ParseOrZero(CSpace20.Text)
                    },
                    new() {
                        BayCode = "D", BayId = BayIdFor("D"),
                        Examine = ParseOrZero(DExamine.Text),
                        NotExamine = ParseOrZero(DNotExamine.Text),
                        Space40ft = ParseOrZero(DSpace40.Text),
                        Space20ft = ParseOrZero(DSpace20.Text)
                    },
                };

                var bayOut = ParseOrZero(BayOutEntry.Text);

                // TODO: replace null with the logged-in user's Guid once login is wired up
                await SupabaseService.Instance.SaveBatchAsync(inputs, bayOut, recordedAt, null);

                StatusLabel.TextColor = Colors.Green;
                StatusLabel.Text = "Saved successfully.";
            }
            catch (Exception ex)
            {
                StatusLabel.TextColor = Colors.Red;
                StatusLabel.Text = $"Save failed: {ex.Message}";
            }
            finally
            {
                SaveButton.IsEnabled = true;
            }
        }
    }
}
