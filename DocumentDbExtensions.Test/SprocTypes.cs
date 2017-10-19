using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Diagnostics.UTC
{
    // only wrapped because of stylecop... sigh
    public static class SprocTypes
    {
        public class IncrementCabCountResponse
        {
            public bool Succeeded { get; set; }
            public int OldCount { get; set; }
            public int NewCount { get; set; }
            public string Message { get; set; }
        }

        public class UpdateAndRecalculateDetailedStatusResponse
        {
            public List<string> Errors;

            public int UpdatedScenarios { get; set; }
            public int SkippedScenarios { get; set; }

            public int UpdatedFlights { get; set; }
            public int SkippedFlights { get; set; }

            public int AddedCategories { get; set; }
            public int UpdatedCategories { get; set; }
            public int SkippedCategories { get; set; }

            public int AddedSnapshotSeries { get; set; }
            public int UpdatedSnapshotSeries { get; set; }
            public int SkippedSnapshotSeries { get; set; }

            public int AddedSnapshots { get; set; }
            public int DuplicateSnapshots { get; set; }
            public int SkippedSnapshots { get; set; }

            public int AddedWindowSeries { get; set; }
            public int UpdatedWindowSeries { get; set; }
            public int SkippedWindowSeries { get; set; }

            public int AddedWindows { get; set; }
            public int DuplicateWindows { get; set; }
            public int SkippedWindows { get; set; }

            public int SkippedScenarioStatusUpdates { get; set; }
            public int CompletedScenarioStatusUpdates { get; set; }
        }
    }
}
