using System;
using System.Runtime.CompilerServices;
using Fixes.Boostrap;
using SHCDESE.API;
using SHCDESE.API.Logging;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.MapLoader;
using SHCDESE.Interop;
using SHCDESE.Interop.Query;

namespace Fixes.Events;

internal class FixesMapEvents
{
	private static readonly Lazy<ModLogHelper> _log = new Lazy<ModLogHelper>(() => ModLoggerFactory.CreateHelper("Fixes"));

	private static ModLogHelper Log => _log.Value;

	public static void OnStartMap(MapStartEventArgs e)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		if ((int)((EventHookBase)e).Phase == 0)
		{
			return;
		}
		UnityMainThreadDispatcher.Instance.Enqueue((Action)delegate
		{
			//IL_0028: Unknown result type (might be due to invalid IL or missing references)
			//IL_002d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0035: Unknown result type (might be due to invalid IL or missing references)
			//IL_003a: Unknown result type (might be due to invalid IL or missing references)
			//IL_0053: Unknown result type (might be due to invalid IL or missing references)
			//IL_0058: Unknown result type (might be due to invalid IL or missing references)
			if (Plugin.Instance.LobbySettingsViewModel.DisableAllAnimals)
			{
				GameUnitManagerAPI unitApi = GameUnitManagerAPI.Instance;
				GameStructQuery<GameUnit> val = unitApi.QueryUnits().Where(UnitPredicates.IsAlive);
				eChimps[] array = new eChimps[7];
				RuntimeHelpers.InitializeArray(array, (RuntimeFieldHandle)/*OpCode not supported: LdMemberToken*/);
				val.Where(UnitPredicates.IsOfAnyType((eChimps[])(object)array)).ForEach((RefAction<GameUnit>)delegate(in GameUnit unit, int unitIndex)
				{
					int num = unitIndex + 1;
					unitApi.DeleteUnitSafe(num);
				});
			}
		});
	}
}
