using System;
using System.Collections.Generic;
using System.Linq;

namespace ExtendedData.Core
{
    // Definitions remain complete; only the visible page owns UI objects.
    public sealed class DeferredSettingsPage<TDefinition, TRow>
    {
        public const int PageSize = 64;
        private readonly TDefinition[] definitions;
        private readonly Func<TDefinition, string, bool> matches;
        private readonly Func<TDefinition, TRow> create;
        private TDefinition[] filtered;
        private TRow[] rows;
        private string filter = "";
        private int page;
        private bool expanded;
        public DeferredSettingsPage(IEnumerable<TDefinition> definitions, Func<TDefinition, string, bool> matches, Func<TDefinition, TRow> create)
        {
            this.definitions = definitions.ToArray();
            this.matches = matches;
            this.create = create;
            filtered = this.definitions;
        }
        public IReadOnlyList<TDefinition> Definitions => Array.AsReadOnly(definitions);
        public int Count => definitions.Length;
        public int FilteredCount => filtered.Length;
        public int PageCount => Math.Max(1, (filtered.Length + PageSize - 1) / PageSize);
        public int CreatedRows { get; private set; }
        public bool Expanded { get => expanded; set { if (expanded == value) return; expanded = value; rows = null; } }
        public int Page { get => page; set { int next = Math.Max(0, Math.Min(value, PageCount - 1)); if (page == next) return; page = next; rows = null; } }
        public string Filter
        {
            get => filter;
            set
            {
                value = value ?? "";
                if (filter == value) return;
                filter = value;
                filtered = string.IsNullOrWhiteSpace(value) ? definitions : definitions.Where(x => matches(x, value)).ToArray();
                page = 0; rows = null;
            }
        }
        public TRow[] Rows
        {
            get
            {
                if (!expanded) return Array.Empty<TRow>();
                if (rows == null)
                {
                    rows = filtered.Skip(page * PageSize).Take(PageSize).Select(create).ToArray();
                    CreatedRows += rows.Length;
                }
                return rows;
            }
        }
        public IEnumerable<TRow> MaterializedRows => rows ?? Array.Empty<TRow>();
    }
}
