using AiDev.Executors;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using System.Collections.ObjectModel;

namespace AiDev.WinUI.ViewModels;

public partial class TemplatesViewModel : ObservableObject
{
    private readonly AgentTemplatesService _templatesService;
    private readonly IModelRegistry _modelRegistry;

    [ObservableProperty] public partial bool IsLoading { get; set; }
    [ObservableProperty] public partial string ErrorMessage { get; set; } = "";
    [ObservableProperty] public partial bool Saved { get; set; }
    [ObservableProperty] public partial bool IsDirty { get; set; }

    [ObservableProperty] public partial AgentTemplate? SelectedTemplate { get; set; }
    [ObservableProperty] public partial bool IsNewTemplate { get; set; }

    [ObservableProperty] public partial string EditSlug { get; set; } = "";
    [ObservableProperty] public partial string EditName { get; set; } = "";
    [ObservableProperty] public partial string EditRole { get; set; } = "developer";
    [ObservableProperty] public partial string EditModel { get; set; } = "";
    [ObservableProperty] public partial string EditExecutor { get; set; } = AgentExecutorName.Default.Value;
    [ObservableProperty] public partial string EditDescription { get; set; } = "";
    [ObservableProperty] public partial string EditContent { get; set; } = "";

    private string _baselineSlug = "";
    private string _baselineName = "";
    private string _baselineRole = "";
    private string _baselineModel = "";
    private string _baselineExecutor = "";
    private string _baselineDescription = "";
    private string _baselineContent = "";

    public ObservableCollection<AgentTemplate> Templates { get; } = [];
    public ObservableCollection<string> AvailableExecutors { get; } = [];
    public ObservableCollection<string> AvailableModels { get; } = [];

    public TemplatesViewModel(AgentTemplatesService templatesService, IModelRegistry modelRegistry)
    {
        _templatesService = templatesService;
        _modelRegistry = modelRegistry;

        foreach (var executor in AgentExecutorName.Supported)
            AvailableExecutors.Add(executor.Value);
    }

    [RelayCommand]
    public Task LoadAsync()
    {
        IsLoading = true;
        try
        {
            var currentSlug = SelectedTemplate?.Slug.Value;

            Templates.Clear();
            foreach (var template in _templatesService.ListTemplates())
                Templates.Add(template);

            if (!string.IsNullOrWhiteSpace(currentSlug))
                SelectedTemplate = Templates.FirstOrDefault(t => t.Slug.Value == currentSlug);

            if (SelectedTemplate is null && Templates.Count > 0)
                SelectedTemplate = Templates[0];

            if (SelectedTemplate is null)
                NewTemplate();

            return Task.CompletedTask;
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public void NewTemplate()
    {
        IsNewTemplate = true;
        SelectedTemplate = null;
        ErrorMessage = "";
        Saved = false;

        EditSlug = "";
        EditName = "";
        EditRole = "developer";
        EditExecutor = AgentExecutorName.Default.Value;
        EditDescription = "";
        EditContent = "";

        CaptureBaseline();
        RefreshModels(EditExecutor);
        if (AvailableModels.Count > 0)
            EditModel = AvailableModels[0];
        else
            EditModel = "";
    }

    [RelayCommand]
    public async Task SaveTemplateAsync()
    {
        ErrorMessage = "";
        Saved = false;

        if (string.IsNullOrWhiteSpace(EditName))
        {
            ErrorMessage = "Template name is required.";
            return;
        }

        if (string.IsNullOrWhiteSpace(EditSlug))
        {
            ErrorMessage = "Template slug is required.";
            return;
        }

        if (!AgentSlug.TryParse(EditSlug, out var slug))
        {
            ErrorMessage = "Template slug must contain only lowercase letters, digits, and hyphens.";
            return;
        }

        if (string.IsNullOrWhiteSpace(EditModel))
        {
            ErrorMessage = "Template model is required.";
            return;
        }

        if (!AgentExecutorName.TryParse(EditExecutor, out var executor))
            executor = AgentExecutorName.Default;

        var template = new AgentTemplate
        {
            Slug = slug,
            Name = EditName.Trim(),
            Role = string.IsNullOrWhiteSpace(EditRole) ? "generic" : EditRole.Trim(),
            Model = EditModel.Trim(),
            Executor = executor.Value,
            Description = EditDescription.Trim(),
            Content = EditContent
        };

        if (IsNewTemplate)
            _templatesService.CreateTemplate(template);
        else
            _templatesService.SaveTemplate(template);

        await LoadAsync();
        SelectedTemplate = Templates.FirstOrDefault(t => t.Slug == slug);
        IsNewTemplate = false;
        Saved = true;
        CaptureBaseline();
        UpdateIsDirty();
    }

    [RelayCommand]
    public async Task DeleteTemplateAsync()
    {
        ErrorMessage = "";
        Saved = false;

        if (string.IsNullOrWhiteSpace(EditSlug))
            return;

        _templatesService.DeleteTemplate(EditSlug);
        await LoadAsync();

        if (Templates.Count == 0)
            NewTemplate();
    }

    partial void OnSelectedTemplateChanged(AgentTemplate? value)
    {
        if (value is null)
            return;

        IsNewTemplate = false;
        ErrorMessage = "";
        Saved = false;

        EditSlug = value.Slug.Value;
        EditName = value.Name;
        EditRole = value.Role;
        EditExecutor = string.IsNullOrWhiteSpace(value.Executor)
            ? AgentExecutorName.Default.Value
            : value.Executor;
        EditDescription = value.Description;
        EditContent = value.Content;

        RefreshModels(EditExecutor);
        EditModel = value.Model;
        CaptureBaseline();
        UpdateIsDirty();
        if (AvailableModels.Count > 0 && !AvailableModels.Contains(EditModel, StringComparer.OrdinalIgnoreCase))
            EditModel = AvailableModels[0];
    }

    partial void OnEditExecutorChanged(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return;

        RefreshModels(value);

        if (AvailableModels.Count > 0 && !AvailableModels.Contains(EditModel, StringComparer.OrdinalIgnoreCase))
            EditModel = AvailableModels[0];

        UpdateIsDirty();
    }

    partial void OnEditSlugChanged(string value) => UpdateIsDirty();
    partial void OnEditNameChanged(string value) => UpdateIsDirty();
    partial void OnEditRoleChanged(string value) => UpdateIsDirty();
    partial void OnEditModelChanged(string value) => UpdateIsDirty();
    partial void OnEditDescriptionChanged(string value) => UpdateIsDirty();
    partial void OnEditContentChanged(string value) => UpdateIsDirty();

    private void CaptureBaseline()
    {
        _baselineSlug = EditSlug;
        _baselineName = EditName;
        _baselineRole = EditRole;
        _baselineModel = EditModel;
        _baselineExecutor = EditExecutor;
        _baselineDescription = EditDescription;
        _baselineContent = EditContent;
    }

    private void UpdateIsDirty()
    {
        IsDirty = !string.Equals(EditSlug, _baselineSlug, StringComparison.Ordinal)
            || !string.Equals(EditName, _baselineName, StringComparison.Ordinal)
            || !string.Equals(EditRole, _baselineRole, StringComparison.Ordinal)
            || !string.Equals(EditModel, _baselineModel, StringComparison.Ordinal)
            || !string.Equals(EditExecutor, _baselineExecutor, StringComparison.Ordinal)
            || !string.Equals(EditDescription, _baselineDescription, StringComparison.Ordinal)
            || !string.Equals(EditContent, _baselineContent, StringComparison.Ordinal);
    }

    private void RefreshModels(string executor)
    {
        AvailableModels.Clear();

        if (string.IsNullOrWhiteSpace(executor))
            return;

        foreach (var model in _modelRegistry.GetModelsForExecutor(executor))
            AvailableModels.Add(model.Id);
    }
}
