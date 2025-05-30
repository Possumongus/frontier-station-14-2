using Content.Shared.StationRecords;
using Robust.Client.AutoGenerated;
using Robust.Client.UserInterface.CustomControls;
using Robust.Client.UserInterface.XAML;
using Robust.Shared.Prototypes; // Frontier
using Content.Shared.Roles; // Frontier
using Robust.Shared.Utility; // Frontier
using Content.Client._NF.StationRecords; // Frontier

namespace Content.Client.StationRecords;

[GenerateTypedNameReferences]
public sealed partial class GeneralStationRecordConsoleWindow : DefaultWindow
{
    [Dependency] private readonly IPrototypeManager _prototype = default!; // Frontier

    public Action<uint?>? OnKeySelected;

    public Action<StationRecordFilterType, string>? OnFiltersChanged;
    public Action<uint>? OnDeleted;

    public event Action<ProtoId<JobPrototype>>? OnJobAdd; // Frontier
    public event Action<ProtoId<JobPrototype>>? OnJobSubtract; // Frontier
    public event Action<string>? OnAdvertisementChanged; // Frontier
    private string? _lastAdvertisement; // Frontier
    private bool _advertisementEdited; // Frontier
    public const int MaxAdvertisementLength = 500; // Frontier

    private bool _isPopulating;

    private StationRecordFilterType _currentFilterType;

    public GeneralStationRecordConsoleWindow()
    {
        RobustXamlLoader.Load(this);
        IoCManager.InjectDependencies(this); // Frontier

        _currentFilterType = StationRecordFilterType.Name;

        foreach (var item in Enum.GetValues<StationRecordFilterType>())
        {
            StationRecordsFilterType.AddItem(GetTypeFilterLocals(item), (int)item);
        }

        RecordListing.OnItemSelected += args =>
        {
            if (_isPopulating || RecordListing[args.ItemIndex].Metadata is not uint cast)
                return;

            OnKeySelected?.Invoke(cast);
        };

        RecordListing.OnItemDeselected += _ =>
        {
            if (!_isPopulating)
                OnKeySelected?.Invoke(null);
        };

        StationRecordsFilterType.OnItemSelected += eventArgs =>
        {
            var type = (StationRecordFilterType) eventArgs.Id;

            if (_currentFilterType != type)
            {
                _currentFilterType = type;
                FilterListingOfRecords();
            }
        };

        StationRecordsFiltersValue.OnTextEntered += args =>
        {
            FilterListingOfRecords(args.Text);
        };

        StationRecordsFilters.OnPressed += _ =>
        {
            FilterListingOfRecords(StationRecordsFiltersValue.Text);
        };

        StationRecordsFiltersReset.OnPressed += _ =>
        {
            StationRecordsFiltersValue.Text = "";
            FilterListingOfRecords();
        };

        // Frontier: station/ship advertisements
        // If ropes can be specified in XAML, push this there.
        AdTextBox.Placeholder = new Rope.Leaf(Loc.GetString("general-station-record-console-ad-default-text", ("size", MaxAdvertisementLength)));
        AdTextBox.OnTextChanged += _ =>
        {
            var ropeEqual = GetAdvertisementString() == _lastAdvertisement;
            _advertisementEdited = !ropeEqual;
            AdUnsavedChanges.Visible = !ropeEqual;
            AdSubmitButton.Disabled = ropeEqual;
        };
        AdSubmitButton.OnPressed += _ =>
        {
            var advertisementText = GetAdvertisementString();

            if (advertisementText != _lastAdvertisement)
            {
                _lastAdvertisement = advertisementText; // Prevent quick-sending dupes.
                OnAdvertisementChanged?.Invoke(advertisementText);
            }
            _advertisementEdited = false;
            AdUnsavedChanges.Visible = false;
            AdSubmitButton.Disabled = true;
        };
        // End Frontier: station/ship advertisements
    }

    public void UpdateState(GeneralStationRecordConsoleState state)
    {
        if (state.Filter != null)
        {
            if (state.Filter.Type != _currentFilterType)
            {
                _currentFilterType = state.Filter.Type;
            }

            if (state.Filter.Value != StationRecordsFiltersValue.Text)
            {
                StationRecordsFiltersValue.Text = state.Filter.Value;
            }
        }

        StationRecordsFilterType.SelectId((int)_currentFilterType);

        // Frontier: job list, ship advertisements
        if (state.JobList != null)
        {
            JobListing.Visible = true;
            PopulateJobsContainer(state.JobList);
        }

        if (state.Advertisement != null)
        {
            AdContainer.Visible = true;
            _lastAdvertisement = state.Advertisement;

            // Overwrite text box contents only if not being edited
            if (!_advertisementEdited && !AdTextBox.HasKeyboardFocus())
                AdTextBox.TextRope = new Rope.Leaf(state.Advertisement);
        }
        // End Frontier: station/ship advertisements

        if (state.RecordListing == null)
        {
            RecordListingStatus.Visible = true;
            RecordListing.Visible = true;
            RecordListingStatus.Text = Loc.GetString("general-station-record-console-empty-state");
            RecordContainer.Visible = true;
            RecordContainerStatus.Visible = true;
            return;
        }

        RecordListingStatus.Visible = false;
        RecordListing.Visible = true;
        RecordContainer.Visible = true;
        PopulateRecordListing(state.RecordListing!, state.SelectedKey);

        RecordContainerStatus.Visible = state.Record == null;

        if (state.Record != null)
        {
            RecordContainerStatus.Visible = state.SelectedKey == null;
            RecordContainerStatus.Text = state.SelectedKey == null
                ? Loc.GetString("general-station-record-console-no-record-found")
                : Loc.GetString("general-station-record-console-select-record-info");
            PopulateRecordContainer(state.Record, state.CanDeleteEntries, state.SelectedKey);
        }
        else
        {
            RecordContainer.RemoveAllChildren();
        }
    }
    private void PopulateRecordListing(Dictionary<uint, string> listing, uint? selected)
    {
        RecordListing.Clear();
        RecordListing.ClearSelected();

        _isPopulating = true;

        foreach (var (key, name) in listing)
        {
            var item = RecordListing.AddItem(name);
            item.Metadata = key;
            item.Selected = key == selected;
        }

        _isPopulating = false;

        RecordListing.SortItemsByText();
    }

    private void PopulateRecordContainer(GeneralStationRecord record, bool enableDelete, uint? id)
    {
        RecordContainer.RemoveAllChildren();
        var newRecord = new GeneralRecord(record, enableDelete, id);
        newRecord.OnDeletePressed = OnDeleted;

        RecordContainer.AddChild(newRecord);
    }

    private void FilterListingOfRecords(string text = "")
    {
        if (!_isPopulating)
        {
            OnFiltersChanged?.Invoke(_currentFilterType, text);
        }
    }

    private string GetTypeFilterLocals(StationRecordFilterType type)
    {
        return Loc.GetString($"general-station-record-{type.ToString().ToLower()}-filter");
    }

    // Frontier: job container
    private void PopulateJobsContainer(IReadOnlyDictionary<ProtoId<JobPrototype>, int?> jobList)
    {
        JobListing.RemoveAllChildren();
        foreach (var (job, amount) in jobList)
        {
            // Skip overflow jobs.
            if (amount < 0 || amount is null)
                continue;

            // Get proper job names when possible
            string jobName;
            if (_prototype.TryIndex(job, out var jobProto))
                jobName = jobProto.LocalizedName;
            else
                jobName = job;

            var jobEntry = new JobRow()
            {
                JobName = { Text = jobName },
                JobAmount = { Text = amount.ToString() },
            };
            jobEntry.DecreaseJobSlot.OnPressed += (args) => { OnJobSubtract?.Invoke(job); };
            jobEntry.IncreaseJobSlot.OnPressed += (args) => { OnJobAdd?.Invoke(job); };
            JobListing.AddChild(jobEntry);
        }
    }

    private string GetAdvertisementString()
    {
        var advertisementText = Rope.Collapse(AdTextBox.TextRope);
        if (advertisementText.Length > 500)
            advertisementText = advertisementText.Substring(0, 500);
        return advertisementText;
    }
    // End Frontier: job container
}
