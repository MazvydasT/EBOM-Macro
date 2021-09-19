using PropertyChanged;

namespace EBOM_Macro.States
{
    [AddINotifyPropertyChangedInterface]
    public class StatsState
    {
        public int UnchangedAssemblies { get; set; }
        public int ModifiedAssemblies { get; set; }
        public int NewAssemblies { get; set; }
        public int DeletedAssemblies { get; set; }

        public int UnchangedParts { get; set; }
        public int ModifiedParts { get; set; }
        public int NewParts { get; set; }
        public int DeletedParts { get; set; }

        public int UnchangedItems => UnchangedAssemblies + UnchangedParts;
        public int ModifiedItems => ModifiedAssemblies + ModifiedParts;
        public int NewItems => NewAssemblies + NewParts;
        public int DeletedItems => DeletedAssemblies + DeletedParts;

        public int TotalAssemblies => UnchangedAssemblies + ModifiedAssemblies + NewAssemblies + DeletedAssemblies;
        public int TotalParts => UnchangedParts + ModifiedParts + NewParts + DeletedParts;

        public int GrandTotal => TotalAssemblies + TotalParts;

        public int SelectedUnchangedAssemblies { get; set; }
        public int SelectedModifiedAssemblies { get; set; }
        public int SelectedNewAssemblies { get; set; }
        public int SelectedDeletedAssemblies { get; set; }

        public int SelectedUnchangedParts { get; set; }
        public int SelectedModifiedParts { get; set; }
        public int SelectedNewParts { get; set; }
        public int SelectedDeletedParts { get; set; }

        public int SelectedUnchangedItems => SelectedUnchangedAssemblies + SelectedUnchangedParts;
        public int SelectedModifiedItems => SelectedModifiedAssemblies + SelectedModifiedParts;
        public int SelectedNewItems => SelectedNewAssemblies + SelectedNewParts;
        public int SelectedDeletedItems => SelectedDeletedAssemblies + SelectedDeletedParts;

        public int SelectedTotalAssemblies => SelectedUnchangedAssemblies + SelectedModifiedAssemblies + SelectedNewAssemblies + SelectedDeletedAssemblies;
        public int SelectedTotalParts => SelectedUnchangedParts + SelectedModifiedParts + SelectedNewParts + SelectedDeletedParts;

        public int SelectedGrandTotal => SelectedTotalAssemblies + SelectedTotalParts;

        public object[][] AsTable => new[]
        {
            new object[]
            {
                null,
                "Assemblies",
                "Parts",
                "Total"
            },

            new object[]
            {
                "Unchanged",
                (SelectedUnchangedAssemblies, UnchangedAssemblies),
                (SelectedUnchangedParts, UnchangedParts),
                (SelectedUnchangedItems, UnchangedItems)
            },

            new object[]
            {
                "Modified",
                (SelectedModifiedAssemblies, ModifiedAssemblies),
                (SelectedModifiedParts, ModifiedParts),
                (SelectedModifiedItems, ModifiedItems)
            },

            new object[]
            {
                "New",
                (SelectedNewAssemblies, NewAssemblies),
                (SelectedNewParts, NewParts),
                (SelectedNewItems, NewItems)
            },

            new object[]
            {
                "Deleted",
                (SelectedDeletedAssemblies, DeletedAssemblies),
                (SelectedDeletedParts, DeletedParts),
                (SelectedDeletedItems, DeletedItems)
            },

            new object[]
            {
                "Total",
                (SelectedTotalAssemblies, TotalAssemblies),
                (SelectedTotalParts, TotalParts),
                (SelectedGrandTotal, GrandTotal)
            }
        };
    }
}
