using AiDev.WinUI.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;

namespace AiDev.WinUI.Views.Pages;

public sealed partial class DigestPage : Page
{
    public DigestViewModel ViewModel { get; }

    public DigestPage()
    {
        InitializeComponent();
        ViewModel = App.Services.GetRequiredService<DigestViewModel>();
        DataContext = ViewModel;
        Loaded += (_, _) =>
        {
            ViewModel.Load();
            UpdateStats();
        };
        ViewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(DigestViewModel.TotalMessages)
                or nameof(DigestViewModel.PendingDecisions)
                or nameof(DigestViewModel.ResolvedDecisions)
                or nameof(DigestViewModel.DigestDate))
                UpdateStats();
        };
    }

    private void UpdateStats()
    {
        DigestDateText.Text = string.IsNullOrEmpty(ViewModel.DigestDate)
            ? "" : $"Summary for {ViewModel.DigestDate}";
        TotalMessagesText.Text = ViewModel.TotalMessages.ToString();
        PendingDecisionsText.Text = ViewModel.PendingDecisions.ToString();
        ResolvedDecisionsText.Text = ViewModel.ResolvedDecisions.ToString();
    }
}
