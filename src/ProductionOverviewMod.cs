using System;
using Mafi;
using Mafi.Collections;
using Mafi.Core.Game;
using Mafi.Core.GameLoop;
using Mafi.Core.Mods;
using Mafi.Core.Prototypes;
using Mafi.Unity.InputControl;
using UnityEngine;

namespace ProductionOverview;

public sealed class ProductionOverviewMod : IMod, IDisposable
{
	public ModManifest Manifest { get; }

	public bool IsUiOnly => false;

	public Option<IConfig> ModConfig => Option<IConfig>.None;

	public ModJsonConfig JsonConfig { get; }

	/// <summary>Opens the window. Since game 0.8.8 mods register their own key bindings.</summary>
	internal static KbHandle ToggleShortcut { get; private set; }

	public ProductionOverviewMod(ModManifest manifest)
	{
		Manifest = manifest;
		JsonConfig = new ModJsonConfig(this);
	}

	public void RegisterPrototypes(ProtoRegistrator registrator)
	{
	}

	public void RegisterDependencies(DependencyResolverBuilder depBuilder, ProtosDb protosDb, bool gameWasLoaded)
	{
		depBuilder.RegisterDependency<ProductionStatsCollector>().AsSelf();
		depBuilder.RegisterDependency<ProductionOverviewWindow>().AsSelf();
		depBuilder.RegisterDependency<ProductionOverviewController>().AsSelf();
	}

	public void EarlyInit(DependencyResolver resolver)
	{
	}

	public void Initialize(DependencyResolver resolver, bool gameWasLoaded)
	{
		Tr.Init();
		ToggleShortcut = KbHandleBuilder.Start("ProductionOverview.ToggleWindow", Tr.WindowTitle.Value)
			.SetPrimary(KeyCode.F9)
			.Build();
		resolver.Resolve<IShortcutsRegistry>().Add(this, ToggleShortcut);
		resolver.Resolve<IGameLoopEvents>().RegisterRendererInitState(this, delegate
		{
			resolver.Resolve<ProductionOverviewController>();
		});
	}

	public void MigrateJsonConfig(VersionSlim savedVersion, Dict<string, object> savedValues)
	{
	}

	public void Dispose()
	{
	}
}
