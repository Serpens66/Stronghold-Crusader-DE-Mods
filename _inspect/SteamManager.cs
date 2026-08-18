using System;
using System.Text;
using AOT;
using Steamworks;
using UnityEngine;

[DisallowMultipleComponent]
public class SteamManager : MonoBehaviour
{
	public const int STEAM_APP_ID = 3024040;

	public static AppId_t AppID = new AppId_t(3024040u);

	public static bool s_EverInitialized = false;

	public static SteamManager s_instance;

	public bool m_bInitialized;

	public SteamAPIWarningMessageHook_t m_SteamAPIWarningMessageHook;

	public static SteamManager Instance
	{
		get
		{
			//IL_0012: Unknown result type (might be due to invalid IL or missing references)
			if ((Object)(object)s_instance == (Object)null)
			{
				return new GameObject("SteamManager").AddComponent<SteamManager>();
			}
			return s_instance;
		}
	}

	public static bool Initialized => Instance.m_bInitialized;

	[MonoPInvokeCallback(typeof(SteamAPIWarningMessageHook_t))]
	public static void SteamAPIDebugTextHook(int nSeverity, StringBuilder pchDebugText)
	{
		Debug.LogWarning((object)pchDebugText);
	}

	[RuntimeInitializeOnLoadMethod(/*Could not decode attribute arguments.*/)]
	public static void InitOnPlayMode()
	{
		s_EverInitialized = false;
		s_instance = null;
	}

	public virtual void Awake()
	{
		//IL_0061: Unknown result type (might be due to invalid IL or missing references)
		if ((Object)(object)s_instance != (Object)null)
		{
			Object.Destroy((Object)(object)((Component)this).gameObject);
			return;
		}
		s_instance = this;
		if (s_EverInitialized)
		{
			throw new Exception("Tried to Initialize the SteamAPI twice in one session!");
		}
		Object.DontDestroyOnLoad((Object)(object)((Component)this).gameObject);
		if (!Packsize.Test())
		{
			Debug.LogError((object)"[Steamworks.NET] Packsize Test returned false, the wrong version of Steamworks.NET is being run in this platform.", (Object)(object)this);
		}
		if (!DllCheck.Test())
		{
			Debug.LogError((object)"[Steamworks.NET] DllCheck Test returned false, One or more of the Steamworks binaries seems to be the wrong version.", (Object)(object)this);
		}
		try
		{
			if (SteamAPI.RestartAppIfNecessary(AppID))
			{
				Application.Quit();
				return;
			}
		}
		catch (DllNotFoundException ex)
		{
			Debug.LogError((object)("[Steamworks.NET] Could not load [lib]steam_api.dll/so/dylib. It's likely not in the correct location. Refer to the README for more details.\n" + ex), (Object)(object)this);
			Application.Quit();
			return;
		}
		m_bInitialized = SteamAPI.Init();
		if (!m_bInitialized)
		{
			Debug.LogError((object)"[Steamworks.NET] SteamAPI_Init() failed. Refer to Valve's documentation or the comment above this line for more information.", (Object)(object)this);
			return;
		}
		s_EverInitialized = true;
		Debug.Log((object)"Steam Initialized");
	}

	public virtual void OnEnable()
	{
		//IL_002c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0036: Expected O, but got Unknown
		if ((Object)(object)s_instance == (Object)null)
		{
			s_instance = this;
		}
		if (m_bInitialized && m_SteamAPIWarningMessageHook == null)
		{
			m_SteamAPIWarningMessageHook = new SteamAPIWarningMessageHook_t(SteamAPIDebugTextHook);
			SteamClient.SetWarningMessageHook(m_SteamAPIWarningMessageHook);
		}
	}

	public virtual void OnDestroy()
	{
		if (!((Object)(object)s_instance != (Object)(object)this))
		{
			s_instance = null;
			if (m_bInitialized)
			{
				SteamAPI.Shutdown();
			}
		}
	}

	public virtual void Update()
	{
		if (m_bInitialized)
		{
			SteamAPI.RunCallbacks();
		}
	}
}
