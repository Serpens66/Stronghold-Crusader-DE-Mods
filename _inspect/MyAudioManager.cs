using System;
using System.Collections;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

public class MyAudioManager : MonoBehaviour
{
	public static MyAudioManager instance;

	public const int NUM_SFX_CHANNELS = 20;

	public AudioSource[] sfxSource = (AudioSource[])(object)new AudioSource[20];

	public AudioSource ambientSource1;

	public float ambientSound1_SoundVolume = 1f;

	public float ambientSound1_GameVolume = 1f;

	public bool ambientSound1_IsPlaying;

	public AudioSource ambientSource2;

	public float ambientSound2_SoundVolume = 1f;

	public float ambientSound2_GameVolume = 1f;

	public bool ambientSound2_IsPlaying;

	public int nextSFXChannel;

	public AudioSource speechSource1;

	public AudioClip speechClip1;

	public int speechMode1;

	public bool speech1Units;

	public AudioSource speechSource2;

	public AudioClip speechClip2;

	public int speechMode2;

	public bool speech2Units;

	public bool speechPaused;

	public bool ignoreSpeechMuting;

	public AudioSource speechSource3;

	public AudioClip speechClip3;

	public int speechMode3;

	public AudioSource musicSource;

	public AudioClip musicClip;

	public int musicMode;

	public float nextMusic_SoundVolume = 1f;

	public bool nextMusicLoop;

	public AudioClip nextMusicClip;

	public float music_GameVolume = 1f;

	public float music_SoundVolume = 1f;

	public bool music_faded_for_speech;

	public float music_faded_for_speech_volume = 0.3f;

	public bool musicAboutToLoop;

	public DateTime musicAboutToLoopTime = DateTime.MinValue;

	public bool fadingOutMusic;

	public string fadeOutName = "";

	public DateTime fadeOutStart;

	public float fadeOutSoundVolume;

	public float fadeOutGameVolume;

	public bool fadeOutLoop;

	public bool music_faded_for_speech_forced;

	public static MyAudioManager Instance
	{
		get
		{
			if ((Object)(object)instance == (Object)null)
			{
				instance = Object.FindObjectOfType<MyAudioManager>();
			}
			return instance;
		}
		set
		{
			instance = value;
		}
	}

	public void Awake()
	{
		instance = this;
		Object.DontDestroyOnLoad((Object)(object)((Component)this).gameObject);
		for (int i = 0; i < 20; i++)
		{
			sfxSource[i] = ((Component)this).gameObject.AddComponent<AudioSource>();
		}
		ambientSource1 = ((Component)this).gameObject.AddComponent<AudioSource>();
		ambientSource2 = ((Component)this).gameObject.AddComponent<AudioSource>();
		speechSource1 = ((Component)this).gameObject.AddComponent<AudioSource>();
		speechSource2 = ((Component)this).gameObject.AddComponent<AudioSource>();
		speechSource3 = ((Component)this).gameObject.AddComponent<AudioSource>();
		musicSource = ((Component)this).gameObject.AddComponent<AudioSource>();
	}

	public void Start()
	{
	}

	public void OnDestroy()
	{
		StopAllGameSounds();
	}

	public void Update()
	{
		ambientSound1_IsPlaying = getAmbientSource(1).isPlaying;
		ambientSound2_IsPlaying = getAmbientSource(2).isPlaying;
		MonitorLoadedSounds();
	}

	public void OnApplicationFocus(bool hasFocus)
	{
		if (!ConfigSettings.Settings_BackgroundAudio)
		{
			updateSFXVolumeFromSettings();
			updateSpeechVolumeFromSettings();
			updateMusicVolumeFromSettings();
		}
	}

	public static float GetMasterVolume()
	{
		if (!Application.isFocused && !ConfigSettings.Settings_BackgroundAudio)
		{
			return 0f;
		}
		return ConfigSettings.Settings_MasterVolume;
	}

	public void playSFX(AudioClip sound, float volume, float pan = 0f, bool unstoppable = false, bool force = false)
	{
		float num = volume * ConfigSettings.Settings_SFXVolume * GetMasterVolume();
		if (!(num > 0f))
		{
			return;
		}
		AudioSource freeSFXChannel = getFreeSFXChannel(force);
		if ((Object)(object)freeSFXChannel != (Object)null)
		{
			freeSFXChannel.panStereo = pan;
			freeSFXChannel.PlayOneShot(sound, num);
			if (unstoppable)
			{
				((Object)freeSFXChannel).name = "0";
			}
			else
			{
				((Object)freeSFXChannel).name = "1";
			}
		}
	}

	public AudioSource getFreeSFXChannel(bool force)
	{
		int num = 20;
		do
		{
			int num2 = nextSFXChannel;
			nextSFXChannel++;
			if (nextSFXChannel == 20)
			{
				nextSFXChannel = 0;
			}
			if (!sfxSource[num2].isPlaying)
			{
				return sfxSource[num2];
			}
		}
		while (num-- >= 0);
		if (force)
		{
			sfxSource[nextSFXChannel].Stop();
			return sfxSource[nextSFXChannel];
		}
		return null;
	}

	public void updateSFXVolumeFromSettings()
	{
		if (isAmbientPlaying(1))
		{
			ambientSource1.volume = getAmbientVolume(1) * ConfigSettings.Settings_SFXVolume * GetMasterVolume();
		}
		if (isAmbientPlaying(2))
		{
			ambientSource2.volume = getAmbientVolume(2) * ConfigSettings.Settings_SFXVolume * GetMasterVolume();
		}
	}

	public void playAmbient(int channelID, AudioClip sound, float soundVolume, float gameVolume, bool loop)
	{
		AudioSource ambientSource = getAmbientSource(channelID);
		switch (channelID)
		{
		case 1:
			ambientSound1_SoundVolume = soundVolume;
			ambientSound1_GameVolume = gameVolume;
			break;
		case 2:
			ambientSound2_SoundVolume = soundVolume;
			ambientSound2_GameVolume = gameVolume;
			break;
		}
		float ambientVolume = getAmbientVolume(channelID);
		ambientSource.loop = loop;
		if (loop)
		{
			ambientSource.volume = ambientVolume * ConfigSettings.Settings_SFXVolume * GetMasterVolume();
			ambientSource.clip = sound;
			ambientSource.Play();
		}
		else
		{
			ambientSource.volume = ambientVolume * ConfigSettings.Settings_SFXVolume * GetMasterVolume();
			ambientSource.PlayOneShot(sound);
		}
	}

	public void setAmbientVolume(int channelID, float gameVolume)
	{
		AudioSource ambientSource = getAmbientSource(channelID);
		switch (channelID)
		{
		case 1:
			ambientSound1_GameVolume = gameVolume;
			break;
		case 2:
			ambientSound2_GameVolume = gameVolume;
			break;
		}
		float ambientVolume = getAmbientVolume(channelID);
		ambientSource.volume = ambientVolume * ConfigSettings.Settings_SFXVolume * GetMasterVolume();
	}

	public void stopAmbient(int channelID)
	{
		getAmbientSource(channelID).Stop();
	}

	public bool isAmbientPlaying(int channelID)
	{
		return channelID switch
		{
			1 => ambientSound1_IsPlaying, 
			2 => ambientSound2_IsPlaying, 
			_ => false, 
		};
	}

	public AudioSource getAmbientSource(int channelID)
	{
		return (AudioSource)(channelID switch
		{
			1 => ambientSource1, 
			2 => ambientSource2, 
			_ => ambientSource1, 
		});
	}

	public float getAmbientVolume(int channelID)
	{
		return channelID switch
		{
			1 => ambientSound1_GameVolume * ambientSound1_SoundVolume, 
			2 => ambientSound2_GameVolume * ambientSound2_SoundVolume, 
			_ => 1f, 
		};
	}

	public void PlaySpeech(int channel, string folder, string soundName, bool force = false, bool unitsSpeech = false, bool _ignoreSpeechMuting = false, bool ignorePauseState = false)
	{
		ignoreSpeechMuting = _ignoreSpeechMuting;
		switch (channel)
		{
		case 1:
			if (force && speechMode1 != 0)
			{
				speechSource1.Stop();
				if ((Object)(object)speechClip1 != (Object)null)
				{
					speechClip1.UnloadAudioData();
					speechClip1 = null;
				}
				speechMode1 = 0;
			}
			if (speechMode1 == 0)
			{
				speechMode1 = 1;
				speech1Units = unitsSpeech;
				LoadClip(channel, folder, soundName, unitsSpeech, ignorePauseState);
			}
			break;
		case 2:
			if (force && speechMode2 != 0)
			{
				speechSource2.Stop();
				if ((Object)(object)speechClip2 != (Object)null)
				{
					speechClip2.UnloadAudioData();
					speechClip2 = null;
				}
				speechMode2 = 0;
			}
			if (speechMode2 == 0)
			{
				speechMode2 = 1;
				speech2Units = unitsSpeech;
				LoadClip(channel, folder, soundName, unitsSpeech, ignorePauseState);
			}
			break;
		case 3:
			if (force && speechMode3 != 0)
			{
				speechSource3.Stop();
				if ((Object)(object)speechClip3 != (Object)null)
				{
					speechClip3.UnloadAudioData();
					speechClip3 = null;
				}
				speechMode3 = 0;
			}
			if (speechMode3 == 0)
			{
				speechMode3 = 1;
				LoadClip(channel, folder, soundName, unitsSpeech: false);
			}
			break;
		}
	}

	public async void LoadClip(int channel, string folder, string soundName, bool unitsSpeech, bool ignorePauseState = false)
	{
		string path = ((folder == "*") ? soundName : ((ConfigSettings.Settings_EnglishSpeech || FatControler.UsesEnglishSpeechFolder()) ? Path.Combine(Application.streamingAssetsPath, "EnglishSpeech", folder, soundName) : Path.Combine(Application.dataPath, "Assets", "GUI", "Speech", folder, soundName)));
		switch (channel)
		{
		case 1:
		{
			speechClip1 = await LoadClip(path);
			float volume3 = ConfigSettings.Settings_SpeechVolume * GetMasterVolume();
			if (unitsSpeech)
			{
				volume3 = ConfigSettings.Settings_UnitSpeechVolume * GetMasterVolume();
			}
			speechSource1.volume = volume3;
			speechSource1.PlayOneShot(speechClip1);
			speechMode1 = 2;
			if (speechPaused && !ignorePauseState)
			{
				speechSource1.Pause();
			}
			else
			{
				setMusicFadedState(ConfigSettings.Settings_ReduceMusicVolumeForSpeech && !ignoreSpeechMuting);
			}
			break;
		}
		case 2:
		{
			speechClip2 = await LoadClip(path);
			float volume2 = ConfigSettings.Settings_SpeechVolume * GetMasterVolume();
			if (unitsSpeech)
			{
				volume2 = ConfigSettings.Settings_UnitSpeechVolume * GetMasterVolume();
			}
			speechSource2.volume = volume2;
			speechSource2.PlayOneShot(speechClip2);
			speechMode2 = 2;
			if (speechPaused && !ignorePauseState)
			{
				speechSource2.Pause();
			}
			else
			{
				setMusicFadedState(ConfigSettings.Settings_ReduceMusicVolumeForSpeech && !ignoreSpeechMuting);
			}
			break;
		}
		case 3:
		{
			speechClip3 = await LoadClip(path);
			float volume = ConfigSettings.Settings_SpeechVolume * GetMasterVolume();
			speechSource3.volume = volume;
			speechSource3.PlayOneShot(speechClip3);
			speechMode3 = 2;
			setMusicFadedState(ConfigSettings.Settings_ReduceMusicVolumeForSpeech && !ignoreSpeechMuting);
			break;
		}
		}
	}

	public void PauseSpeech(bool state)
	{
		speechPaused = state;
		if (state)
		{
			if (speechMode1 == 2)
			{
				speechSource1.Pause();
			}
			if (speechMode2 == 2)
			{
				speechSource2.Pause();
			}
			return;
		}
		if (speechMode1 == 2)
		{
			speechSource1.UnPause();
			setMusicFadedState(ConfigSettings.Settings_ReduceMusicVolumeForSpeech && !ignoreSpeechMuting);
		}
		if (speechMode2 == 2)
		{
			speechSource2.UnPause();
			setMusicFadedState(ConfigSettings.Settings_ReduceMusicVolumeForSpeech && !ignoreSpeechMuting);
		}
	}

	public async Task<AudioClip> LoadClip(string path)
	{
		AudioClip clip = null;
		UnityWebRequest uwr = UnityWebRequestMultimedia.GetAudioClip(path, (AudioType)20);
		try
		{
			uwr.SendWebRequest();
			try
			{
				while (!uwr.isDone)
				{
					await Task.Delay(5);
				}
				if ((int)uwr.result == 2 || (int)uwr.result == 3)
				{
					Debug.Log((object)(uwr.error + " " + path));
				}
				else
				{
					clip = DownloadHandlerAudioClip.GetContent(uwr);
				}
			}
			catch (Exception ex)
			{
				Debug.Log((object)(ex.Message + ", " + ex.StackTrace));
			}
		}
		finally
		{
			((IDisposable)uwr)?.Dispose();
		}
		return clip;
	}

	public void MonitorLoadedSounds()
	{
		if (!speechPaused)
		{
			if (speechMode1 == 2 && !speechSource1.isPlaying)
			{
				if ((Object)(object)speechClip1 != (Object)null)
				{
					speechClip1.UnloadAudioData();
					speechClip1 = null;
				}
				speechMode1 = 0;
			}
			if (speechMode2 == 2 && !speechSource2.isPlaying)
			{
				if ((Object)(object)speechClip2 != (Object)null)
				{
					speechClip2.UnloadAudioData();
					speechClip2 = null;
				}
				speechMode2 = 0;
			}
			if (speechMode3 == 2 && !speechSource3.isPlaying)
			{
				if ((Object)(object)speechClip3 != (Object)null)
				{
					speechClip3.UnloadAudioData();
					speechClip3 = null;
				}
				speechMode3 = 0;
			}
			if (!music_faded_for_speech_forced)
			{
				setMusicFadedState((speechSource1.isPlaying || speechSource2.isPlaying || speechSource3.isPlaying) && ConfigSettings.Settings_ReduceMusicVolumeForSpeech && !ignoreSpeechMuting);
			}
		}
		if (musicMode != 2)
		{
			return;
		}
		if (fadingOutMusic)
		{
			TimeSpan timeSpan = DateTime.UtcNow - fadeOutStart;
			float num = 1f;
			if (timeSpan.TotalSeconds > (double)num)
			{
				fadingOutMusic = false;
				if (musicSource.isPlaying)
				{
					musicSource.Stop();
				}
				if ((Object)(object)musicClip != (Object)null)
				{
					musicClip.UnloadAudioData();
					musicClip = null;
				}
				musicMode = 0;
				if (fadeOutName.Length > 0)
				{
					PlayMusic(fadeOutName, fadeOutGameVolume, fadeOutSoundVolume, fadeOutLoop);
				}
			}
			else
			{
				float num2 = (num - (float)timeSpan.TotalSeconds) / num;
				musicSource.volume = music_GameVolume * music_SoundVolume * ConfigSettings.Settings_MusicVolume * GetMasterVolume() * getFadedMusicVolume() * num2;
			}
			return;
		}
		if (musicSource.time > musicSource.clip.length - 1f && !musicAboutToLoop && musicAboutToLoopTime < DateTime.UtcNow)
		{
			musicAboutToLoop = true;
			musicAboutToLoopTime = DateTime.UtcNow.AddSeconds(4.0);
		}
		if (!musicSource.isPlaying)
		{
			if ((Object)(object)musicClip != (Object)null)
			{
				musicClip.UnloadAudioData();
				musicClip = null;
			}
			if ((Object)(object)nextMusicClip != (Object)null)
			{
				musicSource.loop = nextMusicLoop;
				musicSource.clip = nextMusicClip;
				musicClip = nextMusicClip;
				nextMusicClip = null;
				musicSource.volume = nextMusic_SoundVolume * music_GameVolume * ConfigSettings.Settings_MusicVolume * GetMasterVolume() * getFadedMusicVolume();
				musicSource.Play();
				musicAboutToLoop = false;
				musicAboutToLoopTime = DateTime.MinValue;
				musicMode = 2;
			}
			else
			{
				musicMode = 0;
			}
		}
	}

	public bool isSpeechPlaying(int channelID)
	{
		return channelID switch
		{
			1 => speechMode1 != 0, 
			2 => speechMode2 != 0, 
			3 => speechMode3 != 0, 
			_ => false, 
		};
	}

	public void updateSpeechVolumeFromSettings()
	{
		if (isSpeechPlaying(1))
		{
			if (speech1Units)
			{
				speechSource1.volume = ConfigSettings.Settings_UnitSpeechVolume * GetMasterVolume();
			}
			else
			{
				speechSource1.volume = ConfigSettings.Settings_SpeechVolume * GetMasterVolume();
			}
		}
		if (isSpeechPlaying(2))
		{
			if (speech2Units)
			{
				speechSource2.volume = ConfigSettings.Settings_UnitSpeechVolume * GetMasterVolume();
			}
			else
			{
				speechSource2.volume = ConfigSettings.Settings_SpeechVolume * GetMasterVolume();
			}
		}
		if (isSpeechPlaying(3))
		{
			speechSource3.volume = ConfigSettings.Settings_SpeechVolume * GetMasterVolume();
		}
	}

	public void FadeOutMusic()
	{
		fadingOutMusic = true;
		fadeOutName = "";
		fadeOutStart = DateTime.UtcNow;
		fadeOutSoundVolume = 0f;
		fadeOutGameVolume = 0f;
		fadeOutLoop = false;
	}

	public void PlayMusic(string soundName, float gameVolume, float soundVolume, bool loop = true, bool followon = false, bool fadeOut = false)
	{
		soundName = soundName.Replace(".raw", ".wav");
		if (musicMode != 0)
		{
			if (!followon)
			{
				if (fadeOut)
				{
					fadingOutMusic = true;
					fadeOutName = soundName;
					fadeOutStart = DateTime.UtcNow;
					fadeOutGameVolume = gameVolume;
					fadeOutSoundVolume = soundVolume;
					fadeOutLoop = loop;
					return;
				}
				musicSource.Stop();
				if ((Object)(object)musicClip != (Object)null)
				{
					musicClip.UnloadAudioData();
					musicClip = null;
				}
				musicMode = 0;
			}
			else
			{
				musicSource.loop = false;
			}
		}
		else if (followon)
		{
			followon = false;
		}
		if (musicMode == 0 || followon)
		{
			if (!followon)
			{
				musicMode = 1;
			}
			LoadMusicClip(soundName, gameVolume, soundVolume, loop, followon);
		}
	}

	public async void LoadMusicClip(string soundName, float gameVolume, float soundVolume, bool loop, bool followon)
	{
		string path = Path.Combine(Application.streamingAssetsPath, "Music", soundName);
		if (!followon)
		{
			musicClip = await LoadClip(path);
			music_GameVolume = gameVolume;
			music_SoundVolume = soundVolume;
			musicSource.loop = loop;
			musicSource.volume = gameVolume * soundVolume * ConfigSettings.Settings_MusicVolume * GetMasterVolume() * getFadedMusicVolume();
			musicSource.clip = musicClip;
			musicSource.Play();
			musicAboutToLoop = false;
			musicAboutToLoopTime = DateTime.MinValue;
			musicMode = 2;
		}
		else
		{
			nextMusicLoop = loop;
			nextMusic_SoundVolume = soundVolume;
			nextMusicClip = await LoadClip(path);
		}
	}

	public bool isMusicPlaying()
	{
		return musicMode != 0;
	}

	public bool isMusicAboutToLoop()
	{
		if (musicAboutToLoop)
		{
			musicAboutToLoop = false;
			return true;
		}
		return false;
	}

	public void setMusicVolume(float gameVolume)
	{
		music_GameVolume = gameVolume;
		musicSource.volume = music_GameVolume * music_SoundVolume * ConfigSettings.Settings_MusicVolume * GetMasterVolume() * getFadedMusicVolume();
	}

	public float getFadedMusicVolume()
	{
		if (music_faded_for_speech)
		{
			return music_faded_for_speech_volume;
		}
		return 1f;
	}

	public void setMusicFadedState(bool faded, float fadedVolume = 0.3f, bool force = false)
	{
		if (music_faded_for_speech != faded)
		{
			music_faded_for_speech_forced = force;
			if (faded && (!speechSource1.isPlaying || speechSource1.volume == 0f) && (!speechSource2.isPlaying || speechSource2.volume == 0f) && (!speechSource3.isPlaying || speechSource3.volume == 0f))
			{
				faded = false;
			}
			music_faded_for_speech = faded;
			music_faded_for_speech_volume = fadedVolume;
			updateMusicVolumeFromSettings();
		}
	}

	public void updateMusicVolumeFromSettings()
	{
		if (isMusicPlaying())
		{
			setMusicVolume(music_GameVolume);
		}
	}

	public void stopMusic()
	{
		SFXManager.instance.lastMusic = "";
		if (musicMode == 2)
		{
			if ((Object)(object)nextMusicClip != (Object)null)
			{
				nextMusicClip.UnloadAudioData();
				nextMusicClip = null;
			}
			if (musicSource.isPlaying)
			{
				musicSource.Stop();
			}
			if ((Object)(object)musicClip != (Object)null)
			{
				musicClip.UnloadAudioData();
				musicClip = null;
			}
			musicMode = 0;
		}
	}

	public void StopSFX()
	{
		AudioSource[] array = sfxSource;
		foreach (AudioSource val in array)
		{
			if (((Object)val).name == "0")
			{
				val.Stop();
			}
		}
	}

	public void StopAllGameSounds(bool leaveMusicPlaying = false)
	{
		ambientSource1.Stop();
		ambientSource2.Stop();
		StopSFX();
		if (speechMode1 >= 1)
		{
			try
			{
				if (speechSource1.isPlaying)
				{
					speechSource1.Stop();
				}
				if ((Object)(object)speechClip1 != (Object)null)
				{
					speechClip1.UnloadAudioData();
					speechClip1 = null;
				}
			}
			catch (Exception)
			{
			}
			speechMode1 = 0;
		}
		if (speechMode2 >= 1)
		{
			try
			{
				if (speechSource2.isPlaying)
				{
					speechSource2.Stop();
				}
				if ((Object)(object)speechClip2 != (Object)null)
				{
					speechClip2.UnloadAudioData();
					speechClip2 = null;
				}
			}
			catch (Exception)
			{
			}
			speechMode2 = 0;
		}
		if (speechMode3 >= 1)
		{
			try
			{
				if (speechSource3.isPlaying)
				{
					speechSource3.Stop();
				}
				if ((Object)(object)speechClip3 != (Object)null)
				{
					speechClip3.UnloadAudioData();
					speechClip3 = null;
				}
			}
			catch (Exception)
			{
			}
			speechMode3 = 0;
		}
		music_faded_for_speech = false;
		if (!leaveMusicPlaying)
		{
			stopMusic();
		}
		speechPaused = false;
	}

	public void StopSpeech(int channel)
	{
		if (channel != 3 || speechMode3 < 1)
		{
			return;
		}
		try
		{
			if (speechSource3.isPlaying)
			{
				speechSource3.Stop();
			}
			if ((Object)(object)speechClip3 != (Object)null)
			{
				speechClip3.UnloadAudioData();
				speechClip3 = null;
			}
		}
		catch (Exception)
		{
		}
		speechMode3 = 0;
	}

	public void delayPlaySpeech(int channel, string fullpath, float volume, bool ignoreSpeechMuting = false)
	{
		((MonoBehaviour)this).StartCoroutine(doDelayPlaySpeech(channel, fullpath, volume, ignoreSpeechMuting));
	}

	public IEnumerator doDelayPlaySpeech(int channel, string fullpath, float volume, bool ignoreSpeechMuting)
	{
		yield return (object)new WaitForSeconds(0.5f);
		SFXManager.instance.playSpeech(channel, fullpath, volume, ignoreSpeechMuting);
	}
}
