using System.Collections.Generic;
using UnityEngine;

namespace ProductionOverview;

/// <summary>
/// Which building types count as production (checkbox in the "By type" tab). Waste disposal types are excluded by
/// default, everything else included; only the player's deviations from that are stored. Kept in the player
/// preferences, so the choice applies to all saves.
/// </summary>
public sealed class HiddenTypes
{
	private const string PREF_KEY = "ProductionOverview_HiddenTypes";

	private const char SEPARATOR = '|';

	/// <summary>Proto id -> included in the overview.</summary>
	private readonly Dictionary<string, bool> m_overrides = new Dictionary<string, bool>();

	public int OverrideCount => m_overrides.Count;

	public HiddenTypes()
	{
		string saved = PlayerPrefs.GetString(PREF_KEY, "");
		foreach (string entry in saved.Split(SEPARATOR))
		{
			if (entry.Length == 0)
			{
				continue;
			}
			// "id=1" / "id=0"; a bare "id" is the format of version 1.0, which only knew hidden types.
			int eq = entry.LastIndexOf('=');
			if (eq < 0)
			{
				m_overrides[entry] = false;
			}
			else
			{
				m_overrides[entry.Substring(0, eq)] = entry.Substring(eq + 1) == "1";
			}
		}
	}

	public bool IsHidden(string protoId, bool isWasteDisposal)
	{
		if (protoId != null && m_overrides.TryGetValue(protoId, out bool included))
		{
			return !included;
		}
		return isWasteDisposal;
	}

	public void SetIncluded(string protoId, bool isWasteDisposal, bool included)
	{
		bool isDefault = included == !isWasteDisposal;
		bool changed = isDefault
			? m_overrides.Remove(protoId)
			: !m_overrides.TryGetValue(protoId, out bool old) || old != included;
		if (!isDefault)
		{
			m_overrides[protoId] = included;
		}
		if (changed)
		{
			save();
		}
	}

	public void Reset()
	{
		m_overrides.Clear();
		save();
	}

	private void save()
	{
		List<string> entries = new List<string>();
		foreach (KeyValuePair<string, bool> kv in m_overrides)
		{
			entries.Add(kv.Key + "=" + (kv.Value ? "1" : "0"));
		}
		PlayerPrefs.SetString(PREF_KEY, string.Join(SEPARATOR.ToString(), entries));
		PlayerPrefs.Save();
	}
}
