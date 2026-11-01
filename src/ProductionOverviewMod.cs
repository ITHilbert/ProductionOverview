using System;
using Mafi;
using Mafi.Collections;
using Mafi.Core.Game;
using Mafi.Core.GameLoop;
using Mafi.Core.Mods;
using Mafi.Core.Prototypes;

namespace ProductionOverview;

public sealed class ProductionOverviewMod : IMod, IDisposable
{
	public ModManifest Manifest { get; }

	public bool IsUiOnly => false;

	public Option<IConfig> ModConfig => Option<IConfig>.None;

	public ModJsonConfig JsonConfig { get; }

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
