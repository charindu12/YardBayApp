using System.Text.RegularExpressions;
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
            // Gate In/Out is normally recorded a day behind the Bay Entry date
            // (containers that move today are usually only counted/confirmed the
            // next day) - both pickers start out matched on that same relationship.
            GateDatePicker.Date = DateTime.Today.AddDays(-1);

            EntryDatePicker.DateSelected += async (_, __) =>
            {
                // Keep Gate Date locked one day behind whichever Bay Entry date is
                // being viewed, so switching Bay Entry to e.g. 2026-08-08 always
                // shows the matching 2026-08-07 Gate In/Out numbers automatically,
                // instead of leaving whatever Gate Date was previously selected.
                // The supervisor can still override Gate Date manually afterwards
                // if a particular day needs a different pairing.
                GateDatePicker.Date = EntryDatePicker.Date.AddDays(-1);
                await LoadExistingForDateAsync();
                await LoadExistingGateInOutAsync();
            };
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

        /// <summary>
        /// Parses a pasted daily report (WhatsApp-style free text, e.g. one bay per
        /// paragraph with "Examine = X" / "Not examine = Y" / "Bay space = Z (40-.. 20-..)"
        /// lines, a "Bay out = N" line, and an optional date/time header) and fills the
        /// matching form fields, instead of the supervisor typing every number by hand.
        /// This is best-effort text parsing - always review the filled numbers before Save,
        /// especially if the report's wording differs from the usual format.
        /// </summary>
        private void OnParseReportClicked(object sender, EventArgs e) => ParseAndFillReport();

        /// <summary>Auto-fills the form the moment text is pasted into the report box,
        /// so the supervisor doesn't need to tap the button separately after pasting.</summary>
        private void OnReportTextChanged(object sender, TextChangedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(e.NewTextValue)) return;
            ParseAndFillReport();
        }

        private void ParseAndFillReport()
        {
            var text = ReportPasteEditor.Text ?? string.Empty;
            if (string.IsNullOrWhiteSpace(text))
            {
                StatusLabel.TextColor = Colors.Red;
                StatusLabel.Text = "Paste the report text first.";
                return;
            }

            var warnings = new List<string>();

            // ---- Date / time header, e.g. "2026/08/08 08.30Am" ----
            var dtMatch = Regex.Match(text, @"(\d{4})/(\d{2})/(\d{2})\s+(\d{1,2})\.(\d{2})\s*([AaPp][Mm])");
            if (dtMatch.Success)
            {
                var year = int.Parse(dtMatch.Groups[1].Value);
                var month = int.Parse(dtMatch.Groups[2].Value);
                var day = int.Parse(dtMatch.Groups[3].Value);
                var hour = int.Parse(dtMatch.Groups[4].Value);
                var minute = int.Parse(dtMatch.Groups[5].Value);
                var ampm = dtMatch.Groups[6].Value.ToUpperInvariant();
                if (ampm == "PM" && hour != 12) hour += 12;
                if (ampm == "AM" && hour == 12) hour = 0;

                try
                {
                    EntryDatePicker.Date = new DateTime(year, month, day);
                    EntryTimePicker.Time = new TimeSpan(hour, minute, 0);
                }
                catch
                {
                    warnings.Add("Could not read the date/time header - Date/Time left as-is.");
                }
            }
            else
            {
                warnings.Add("No date/time header found - Date/Time left as-is.");
            }

            // ---- Per-bay blocks ----
            void FillBay(string code, Entry examineEntry, Entry notExamineEntry, Entry space40Entry, Entry space20Entry)
            {
                // Everything from "<code> bay" up to the next bay letter, "Bay out", "Total", or the end.
                var segMatch = Regex.Match(text,
                    $@"(?is){code}\s*bay(.*?)(?=[ABCD]\s*bay\b|bay\s*out\b|total\b|$)");
                if (!segMatch.Success)
                {
                    warnings.Add($"{code} Bay: section not found in the pasted text.");
                    return;
                }
                var segment = segMatch.Groups[1].Value;

                var notExamineMatch = Regex.Match(segment, @"(?i)not\s*examine\s*=?\s*(\d+)");
                var examineMatch = Regex.Match(segment, @"(?i)(?<!not\s)examine\s*=?\s*(\d+)");
                var spaceMatch = Regex.Match(segment, @"(?i)(?:bay\s*)?space\s*=?\s*(\d+)");
                var space40Match = Regex.Match(segment, @"(?i)40'?\s*[-:xX]?\s*(\d+)");
                var space20Match = Regex.Match(segment, @"(?i)20'?\s*[-:xX]?\s*(\d+)");

                examineEntry.Text = examineMatch.Success ? examineMatch.Groups[1].Value : "0";
                notExamineEntry.Text = notExamineMatch.Success ? notExamineMatch.Groups[1].Value : "0";
                if (!examineMatch.Success) warnings.Add($"{code} Bay: Examine not found.");
                if (!notExamineMatch.Success) warnings.Add($"{code} Bay: Not Examine not found.");

                if (space40Match.Success && space20Match.Success)
                {
                    space40Entry.Text = space40Match.Groups[1].Value;
                    space20Entry.Text = space20Match.Groups[1].Value;
                }
                else
                {
                    space40Entry.Text = "0";
                    space20Entry.Text = "0";
                    if (spaceMatch.Success && int.TryParse(spaceMatch.Groups[1].Value, out var totalSpace) && totalSpace != 0)
                        warnings.Add($"{code} Bay: space total ({totalSpace}) has no 40'/20' breakdown - fill Space 40'/Space 20' manually.");
                }
            }

            FillBay("A", AExamine, ANotExamine, ASpace40, ASpace20);
            FillBay("B", BExamine, BNotExamine, BSpace40, BSpace20);
            FillBay("C", CExamine, CNotExamine, CSpace40, CSpace20);
            FillBay("D", DExamine, DNotExamine, DSpace40, DSpace20);

            // ---- Bay out ----
            var bayOutMatch = Regex.Match(text, @"(?i)bay\s*out\s*=?\s*(\d+)");
            if (bayOutMatch.Success)
                BayOutEntry.Text = bayOutMatch.Groups[1].Value;
            else
                warnings.Add("Bay Out not found.");

            if (warnings.Count == 0)
            {
                StatusLabel.TextColor = Colors.Green;
                StatusLabel.Text = "Filled from the pasted report - please check the numbers, then Save.";
            }
            else
            {
                StatusLabel.TextColor = Colors.Orange;
                StatusLabel.Text = "Filled with some gaps - please check before Save:\n" + string.Join("\n", warnings);
            }
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
                // entry overwrites it instead of creating a duplicate. The returned batchId
                // links this bay submission to the gate entry saved right below, so the
                // dashboard can match them exactly regardless of gate_date or save timing.
                var batchId = await SupabaseService.Instance.SaveOrUpdateBatchAsync(inputs, bayOut, recordedAt, null);

                // Total Gate In and Out Container is saved against its own date (see
                // GateDatePicker), independently of the bay entry date above - but linked
                // to this same batchId so the dashboard knows they belong together.
                var totalGateIn = ParseOrZero(GateInEntry.Text);
                var totalGateOut = ParseOrZero(GateOutEntry.Text);
                await SupabaseService.Instance.SaveOrUpdateGateInOutAsync(GateDatePicker.Date, totalGateIn, totalGateOut, null, batchId);

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
