using System.Collections.ObjectModel;
using YardBayApp.Helpers;
using YardBayApp.Models;
using YardBayApp.Services;

namespace YardBayApp.Views
{
    public class BayLine
    {
        public string BayCode { get; set; } = "";
        public int Examine { get; set; }
        public int NotExamine { get; set; }
        public int Space40 { get; set; }
        public int Space20 { get; set; }
    }

    public class BatchHistoryItem
    {
        public DateTime RecordedAtLocal { get; set; }
        public string RecordedAtLabel => RecordedAtLocal.ToString("dddd, dd MMM yyyy  HH:mm");
        public int BayOut { get; set; }
        public string BayOutLabel => $"Bay Out: {BayOut}";
        public List<BayLine> Bays { get; set; } = new();
    }

    public partial class HistoryPage : ContentPage
    {
        List<Bay> _bays = new();

        public HistoryPage()
        {
            InitializeComponent();
            ToDatePicker.Date = DateTime.Today;
            FromDatePicker.Date = DateTime.Today; // today only by default
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            if (_bays.Count == 0)
            {
                try { _bays = await SupabaseService.Instance.GetBaysAsync(); }
                catch { /* bay codes fall back to "-" below if this fails */ }
            }
            await LoadAsync();
        }

        async void OnFilterClicked(object sender, EventArgs e) => await LoadAsync();

        string BayCodeFor(Guid bayId) => _bays.FirstOrDefault(b => b.Id == bayId)?.Code ?? "-";

        async Task LoadAsync()
        {
            StatusLabel.Text = "";
            FilterButton.IsEnabled = false;
            try
            {
                // Inclusive of the whole "To" day - the picker only gives midnight, so push
                // the upper bound to the last instant of that day before converting to UTC.
                var fromUtc = SriLankaTime.ToUtc(FromDatePicker.Date.Date);
                var toUtc = SriLankaTime.ToUtc(ToDatePicker.Date.Date.AddDays(1).AddSeconds(-1));

                var bayRows = await SupabaseService.Instance.GetHistoryAsync(fromUtc, toUtc);
                var summaryRows = await SupabaseService.Instance.GetYardSummaryHistoryAsync(fromUtc, toUtc);

                var batches = bayRows
                    .GroupBy(r => r.BatchId)
                    .Select(g =>
                    {
                        var summary = summaryRows.FirstOrDefault(s => s.BatchId == g.Key);
                        return new BatchHistoryItem
                        {
                            RecordedAtLocal = SriLankaTime.ToLocal(g.First().RecordedAt),
                            BayOut = summary?.BayOut ?? 0,
                            Bays = g.OrderBy(r => BayCodeFor(r.BayId)).Select(r => new BayLine
                            {
                                BayCode = BayCodeFor(r.BayId),
                                Examine = r.ExamineCount,
                                NotExamine = r.NotExamineCount,
                                Space40 = r.Space40ft,
                                Space20 = r.Space20ft
                            }).ToList()
                        };
                    })
                    .OrderByDescending(b => b.RecordedAtLocal)
                    .ToList();

                BatchList.ItemsSource = new ObservableCollection<BatchHistoryItem>(batches);
            }
            catch (Exception ex)
            {
                StatusLabel.Text = $"Could not load history: {ex.Message}";
                BatchList.ItemsSource = null;
            }
            finally
            {
                FilterButton.IsEnabled = true;
            }
        }
    }
}
