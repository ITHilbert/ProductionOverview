using System;
using System.Collections.Generic;
using System.Reflection;
using Mafi;
using Mafi.Core.Entities;
using Mafi.Core.Entities.Static;
using Mafi.Core.Factory;
using Mafi.Core.Factory.Machines;
using Mafi.Core.Factory.Transports;
using Mafi.Core.Prototypes;

namespace ProductionOverview;

public enum StatsPeriod
{
	CurrentMonth,
	LastMonth,
	Last3Months,
	Last12Months
}

public enum SortMode
{
	EfficiencyAsc,
	EfficiencyDesc,
	Name,
	Type
}

/// <summary>
/// Snapshot of the productivity of one building for the selected period.
/// </summary>
public sealed class BuildingStats
{
	public IStaticEntity Entity;

	public string Name;

	public string TypeName;

	public string IconPath;

	/// <summary>Prototype id, identifies the building type independent of the language.</summary>
	public string ProtoId;

	/// <summary>Smoke stacks, flares, burners, ocean dumps: they only destroy products, their utilization is irrelevant.</summary>
	public bool IsWasteDisposal;

	public bool IsPaused;

	public bool IsConstructed;

	/// <summary>Raw enum name of the current state (e.g. "NotEnoughInput"), or null if the building has none.</summary>
	public string StateName;

	public long Total;

	public long Productive;

	/// <summary>Non-productive categories with their label and sample count, largest first.</summary>
	public readonly List<(string Label, long Count, ColorRgba Color, bool IsOutputFull)> Losses = new List<(string, long, ColorRgba, bool)>();

	/// <summary>The building mostly waits because its output is not picked up (it produces more than needed).</summary>
	public bool MainIssueIsOutputFull => HasData && Losses.Count > 0 && Losses[0].IsOutputFull;

	/// <summary>Less than this many samples (≈ 1/10 of a month) is not enough to judge a building.</summary>
	public const long MIN_SAMPLES = 60;

	public bool HasData => Total >= MIN_SAMPLES;

	/// <summary>0..1, or -1 without data.</summary>
	public float Efficiency => HasData ? (float)Productive / Total : -1f;
}

/// <summary>
/// Reads the productivity counters the game keeps for every production building
/// (the same data the "productivity" charts of the inspector show).
/// </summary>
public sealed class ProductionStatsCollector
{
	private const string DEFAULT_ICON = "Assets/Unity/UserInterface/General/Building.svg";

	/// <summary>"Output full" category of machines (other buildings use category C for different things).</summary>
	private static readonly string OUTPUT_FULL_LABEL_ID = Machine.PROD_COUNTERS_LABELS.CategoryC.Label.Id;

	private readonly IEntitiesManager m_entities;

	private readonly Dictionary<Type, PropertyInfo> m_stateProps = new Dictionary<Type, PropertyInfo>();

	public ProductionStatsCollector(IEntitiesManager entities)
	{
		m_entities = entities;
	}

	public List<BuildingStats> Collect(StatsPeriod period)
	{
		// The simulation runs on another thread and may add/remove entities while we iterate.
		// That is rare, so just retry a few times.
		for (int attempt = 0; ; attempt++)
		{
			try
			{
				return collectOnce(period);
			}
			catch (Exception ex) when (attempt < 3)
			{
				Log.Warning("[ProductionOverview] Retrying collection: " + ex.Message);
			}
		}
	}

	private List<BuildingStats> collectOnce(StatsPeriod period)
	{
		List<BuildingStats> result = new List<BuildingStats>();
		foreach (IEntity entity in m_entities.Entities)
		{
			if (entity is not IEntityWithProductivityCounter counter || entity is not IStaticEntity staticEntity)
			{
				continue; // Vehicles have counters too, but are not buildings.
			}
			if (entity is Transport || entity.IsDestroyed)
			{
				continue; // Conveyors and pipes.
			}
			result.Add(create(counter, staticEntity, period));
		}
		return result;
	}

	private BuildingStats create(IEntityWithProductivityCounter counter, IStaticEntity entity, StatsPeriod period)
	{
		BuildingStats stats = new BuildingStats
		{
			Entity = entity,
			Name = counter.GetTitle(),
			TypeName = entity.Prototype.Strings.Name.TranslatedString,
			IconPath = entity.Prototype is IProtoWithIcon withIcon && !string.IsNullOrEmpty(withIcon.IconPath) ? withIcon.IconPath : DEFAULT_ICON,
			ProtoId = entity.Prototype.Id.Value,
			IsWasteDisposal = entity.Prototype is MachineProto { IsWasteDisposal: true },
			IsPaused = entity.IsPaused,
			IsConstructed = entity.IsConstructed,
			StateName = readState(entity),
		};

		ProductivityCounterHistory.Data data = sum(counter, period);
		ProductivityCounterLabels labels = counter.ProductivityCounterLabels;
		add(stats, data.CategoryA, labels.CategoryA);
		add(stats, data.CategoryB, labels.CategoryB);
		add(stats, data.CategoryC, labels.CategoryC);
		add(stats, data.CategoryD, labels.CategoryD);
		stats.Losses.Sort((a, b) => b.Count.CompareTo(a.Count));
		return stats;
	}

	private static void add(BuildingStats stats, long count, ProductivityLabelCategory category)
	{
		stats.Total += count;
		if (category.IsProductiveMult != 0)
		{
			stats.Productive += count;
		}
		else if (count > 0)
		{
			bool isOutputFull = category.Label.Id == OUTPUT_FULL_LABEL_ID;
			stats.Losses.Add((category.Label.TranslatedString, count, category.Color, isOutputFull));
		}
	}

	private static ProductivityCounterHistory.Data sum(IEntityWithProductivityCounter counter, StatsPeriod period)
	{
		ProductivityCounterHistory history = counter.ProductivityCounterHistory;
		switch (period)
		{
			case StatsPeriod.CurrentMonth:
				return counter.OngoingMonthlyData;
			case StatsPeriod.Last12Months:
				return history.YearlyRolling;
		}

		int months = period == StatsPeriod.LastMonth ? 1 : 3;
		var buffer = history.MonthlyRecordsBuffer;
		int newest = history.MonthlyRecordsBufferIndex;
		uint a = 0, b = 0, c = 0, d = 0;
		for (int i = 0; i < months; i++)
		{
			ProductivityCounterHistory.Data m = buffer[(newest - i + buffer.Length) % buffer.Length];
			a += m.CategoryA;
			b += m.CategoryB;
			c += m.CategoryC;
			d += m.CategoryD;
		}
		// Max 3 * 600 samples per category, fits into ushort.
		return new ProductivityCounterHistory.Data((ushort)a, (ushort)b, (ushort)c, (ushort)d);
	}

	/// <summary>
	/// Most production buildings expose a public <c>CurrentState</c> enum (machines, labs, reactors, ...),
	/// but there is no common interface for it.
	/// </summary>
	private string readState(IStaticEntity entity)
	{
		Type type = entity.GetType();
		if (!m_stateProps.TryGetValue(type, out PropertyInfo prop))
		{
			prop = type.GetProperty("CurrentState", BindingFlags.Public | BindingFlags.Instance);
			if (prop != null && !prop.PropertyType.IsEnum)
			{
				prop = null;
			}
			m_stateProps[type] = prop;
		}
		if (prop == null)
		{
			return null;
		}
		try
		{
			return prop.GetValue(entity)?.ToString();
		}
		catch (Exception)
		{
			return null;
		}
	}
}
