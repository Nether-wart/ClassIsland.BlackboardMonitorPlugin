using ClassIsland.BlackboardMonitorPlugin.Automation;
using ClassIsland.BlackboardMonitorPlugin.Services;
using ClassIsland.BlackboardMonitorPlugin.Views.Automation;
using ClassIsland.BlackboardMonitorPlugin.Views.SettingsPages;
using ClassIsland.Core.Abstractions;
using ClassIsland.Core.Attributes;
using ClassIsland.Core.Extensions.Registry;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ClassIsland.BlackboardMonitorPlugin;

[PluginEntrance]
public class Plugin : PluginBase
{
    public override void Initialize(HostBuilderContext context, IServiceCollection services)
    {
        services.AddSingleton(new BlackboardMonitorSettingsStore(PluginConfigFolder));
        services.AddSingleton<BlackboardMonitorService>();

        services.AddSettingsPage<BlackboardMonitorSettingsPage>();
        services.AddTrigger<BlackboardStateTrigger, BlackboardStateTriggerSettingsControl>();
    }
}
