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
            // Defaults to yesterday: containers that move today are usually only
            // counted/confirmed the next day, so this is normally entered a day behind
            // the bay entry date above. The supervisor can change it either way.
            GateDatePicker.Date = DateTime.Today.AddDays(-1);
            EntryDatePicker.DateSelected += async (_, __) => await LoadExistingForDateAsync();
            GateDatePicker.DateSelected += async (_, __) => await LoadExistingGateInOutAsync();
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

            await LoadExistingForDateAsync();
            await LoadExistingGateInOutAsync();
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

        /// <summary>
        /// Fetches whatever was already saved for the date currently picked and fills the
        /// form with it, so picking an existing date shows that day's real numbers instead
        /// of blank/0 fields. If the field is left blank and saved, it would otherwise wipe
        /// out real data since Save now overwrites the whole day (see SaveOrUpdateBatchAsync).
        /// </summary>
        private async Task LoadExistingForDateAsync()
        {
            void SetBay(string code, int examine, int notExamine, int space40, int space20)
            {
                switch (code)
                {
                    case "A": AExamine.Text = examine.ToString(); ANotExamine.Text = notExamine.ToString(); ASpace40.Text = space40.ToString(); ASpace20.Text = space20.ToString(); break;
                    case "B": BExamine.Text = examine.ToString(); BNotExamine.Text = notExamine.ToString(); BSpace40.Text = space40.ToString(); BSpace20.Text = space20.ToString(); break;
                    case "C": CExamine.Text = examine.ToString(); CNotExamine.Text = notExamine.ToString(); CSpace40.Text = space40.ToString(); CSpace20.Text = space20.ToString(); break;
                    case "D": DExamine.Text = examine.ToString(); DNotExamine.Text = notExamine.ToString(); DSpace40.Text = space40.ToString(); DSpace20.Text = space20.ToString(); break;
                }
            }

            // Clear everything first so a day with no saved entry starts blank, not with
            // whatever was left over from the previously viewed date.
            AExamine.Text = ANotExamine.Text = ASpace40.Text = ASpace20.Text = string.Empty;
            BExamine.Text = BNotExamine.Text = BSpace40.Text = BSpace20.Text = string.Empty;
            CExamine.Text = CNotExamine.Text = CSpace40.Text = CSpace20.Text = string.Empty;
            DExamine.Text = DNotExamine.Text = DSpace40.Text = DSpace20.Text = string.Empty;
            BayOutEntry.Text = string.Empty;

            try
            {
                var (bayRows, summary) = await SupabaseService.Instance.GetEntryForDateAsync(EntryDatePicker.Date);

                foreach (var row in bayRows)
                {
                    var code = _bays.FirstOrDefault(b => b.Id == row.BayId)?.Code;
                    if (code != null)
                        SetBay(code, row.ExamineCount, row.NotExamineCount, row.Space40ft, row.Space20ft);
                }

                if (summary != null)
                {
                    BayOutEntry.Text = summary.BayOut.ToString();
                    EntryTimePicker.Time = SriLankaTime.ToLocal(summary.RecordedAt).TimeOfDay;
                    StatusLabel.TextColor = Colors.Gray;
                    StatusLabel.Text = "Existing entry loaded for this date - Save will overwrite it.";
                }
                else
                {
                    StatusLabel.Text = string.Empty;
                }
            }
            catch (Exception ex)
            {
                StatusLabel.TextColor = Colors.Red;
                StatusLabel.Text = $"Could not check existing entry: {ex.Message}";
            }
        }

        /// <summary>
        /// Loads whatever Total Gate In/Out numbers were already saved for the
        /// currently picked gate date - independent of the bay entry date above.
        /// </summary>
        private async Task LoadExistingGateInOutAsync()
        {
            GateInEntry.Text = string.Empty;
            GateOutEntry.Text = string.Empty;

            try
            {
                var existing = await SupabaseService.Instance.GetGateInOutForDateAsync(GateDatePicker.Date);
                if (existing != null)
                {
                    GateInEntry.Text = existing.TotalGateIn.ToString();
                    GateOutEntry.Text = existing.TotalGateOut.ToString();
                }
            }
            catch (Exception ex)
            {
                StatusLabel.TextColor = Colors.Red;
                StatusLabel.Text = $"Could not check existing gate entry: {ex.Message}";
            }
        }

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
                // Uses SaveOrUpdateBatchAsync so re-saving for a date that already has an
                // entry overwrites it instead of creating a duplicate.
                await SupabaseService.Instance.SaveOrUpdateBatchAsync(inputs, bayOut, recordedAt, null);

                // Total Gate In and Out Container is saved against its own date (see
                // GateDatePicker), independently of the bay entry date above.
                var totalGateIn = ParseOrZero(GateInEntry.Text);
                var totalGateOut = ParseOrZero(GateOutEntry.Text);
                await SupabaseService.Instance.SaveOrUpdateGateInOutAsync(GateDatePicker.Date, totalGateIn, totalGateOut, null);

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
