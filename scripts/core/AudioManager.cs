#nullable enable

using System.Collections.Generic;
using Godot;
using WaterSortGame.Model;

namespace WaterSortGame.Core;

public sealed partial class AudioManager : Node
{
	private const float MuteDb = -80f;
	private const string BgmPlayerName = "BgmPlayer";
	private const string ClickPlayerName = "ClickPlayer";
	private const string PourPlayerName = "PourPlayer";
	private const string BlockedPlayerName = "BlockedPlayer";
	private const string SuccessPlayerName = "SuccessPlayer";

	[Export] public string BgmPath { get; set; } = "res://assets/audio/bgm_pure_morning.mp3";
	[Export] public string ClickPath { get; set; } = "res://assets/audio/sfx_click.wav";
	[Export] public string PourPath { get; set; } = "res://assets/audio/sfx_pour.wav";
	[Export] public string BlockedPath { get; set; } = "res://assets/audio/sfx_blocked.wav";
	[Export] public string SuccessPath { get; set; } = "res://assets/audio/sfx_success.wav";

	private readonly HashSet<string> _warnedMissingPaths = new();
	private AudioStreamPlayer _bgmPlayer = null!;
	private AudioStreamPlayer _clickPlayer = null!;
	private AudioStreamPlayer _pourPlayer = null!;
	private AudioStreamPlayer _blockedPlayer = null!;
	private AudioStreamPlayer _successPlayer = null!;
	private float _musicVolume = SettingsData.DefaultMusicVolume;
	private float _sfxVolume = SettingsData.DefaultSfxVolume;
	private bool _isReady;

	public static AudioManager? Instance { get; private set; }

	public float MusicVolume => _musicVolume;

	public float SfxVolume => _sfxVolume;

	public float MusicVolumeDb => VolumeToDb(_musicVolume);

	public float SfxVolumeDb => VolumeToDb(_sfxVolume);

	public bool HasBgmStream => _isReady && _bgmPlayer.Stream != null;

	public string CurrentBgmPath => _bgmPlayer.Stream == null ? string.Empty : BgmPath;

	public bool HasClickStream => _isReady && _clickPlayer.Stream != null;

	public bool HasPourStream => _isReady && _pourPlayer.Stream != null;

	public bool HasBlockedStream => _isReady && _blockedPlayer.Stream != null;

	public bool HasSuccessStream => _isReady && _successPlayer.Stream != null;

	public override void _EnterTree()
	{
		if (Instance != null && Instance != this)
		{
			GD.PushWarning("Multiple AudioManager nodes entered the tree. The latest node will become the active audio manager.");
		}

		Instance = this;
	}

	public override void _Ready()
	{
		EnsurePlayers();
		LoadStreams();
		ApplyVolumes();
		_isReady = true;
		PlayBgm();
	}

	public override void _ExitTree()
	{
		if (Instance == this)
		{
			Instance = null;
		}
	}

	public void ApplySettings(SettingsData? settings)
	{
		if (settings == null)
		{
			SetMusicVolume(SettingsData.DefaultMusicVolume);
			SetSfxVolume(SettingsData.DefaultSfxVolume);
			return;
		}

		settings.Normalize();
		SetMusicVolume(settings.MusicVolume);
		SetSfxVolume(settings.SfxVolume);
	}

	public void SetMusicVolume(float value)
	{
		_musicVolume = Clamp01(value);
		if (!_isReady)
		{
			return;
		}

		_bgmPlayer.VolumeDb = MusicVolumeDb;
		if (_musicVolume <= 0f)
		{
			_bgmPlayer.Stop();
		}
		else
		{
			PlayBgm();
		}
	}

	public void SetSfxVolume(float value)
	{
		_sfxVolume = Clamp01(value);
		if (_isReady)
		{
			ApplySfxVolume();
		}
	}

	public void PlayBgm()
	{
		if (!_isReady || _bgmPlayer.Stream == null || _musicVolume <= 0f || _bgmPlayer.Playing)
		{
			return;
		}

		_bgmPlayer.Play();
	}

	public void PlayClick()
	{
		PlaySfx(_clickPlayer);
	}

	public void PlayPour()
	{
		PlaySfx(_pourPlayer);
	}

	public void PlayBlocked()
	{
		PlaySfx(_blockedPlayer);
	}

	public void PlaySuccess()
	{
		PlaySfx(_successPlayer);
	}

	public int GetBgmPlayerCount()
	{
		return CountPlayersNamed(BgmPlayerName);
	}

	public int GetSfxPlayerCount()
	{
		return CountPlayersNamed(ClickPlayerName)
			+ CountPlayersNamed(PourPlayerName)
			+ CountPlayersNamed(BlockedPlayerName)
			+ CountPlayersNamed(SuccessPlayerName);
	}

	public static void PlayGlobalClick()
	{
		Instance?.PlayClick();
	}

	public static void PlayGlobalPour()
	{
		Instance?.PlayPour();
	}

	public static void PlayGlobalBlocked()
	{
		Instance?.PlayBlocked();
	}

	public static void PlayGlobalSuccess()
	{
		Instance?.PlaySuccess();
	}

	private void EnsurePlayers()
	{
		_bgmPlayer = EnsurePlayer(BgmPlayerName);
		_clickPlayer = EnsurePlayer(ClickPlayerName);
		_pourPlayer = EnsurePlayer(PourPlayerName);
		_blockedPlayer = EnsurePlayer(BlockedPlayerName);
		_successPlayer = EnsurePlayer(SuccessPlayerName);
		_bgmPlayer.Finished += OnBgmFinished;
	}

	private AudioStreamPlayer EnsurePlayer(string playerName)
	{
		AudioStreamPlayer? player = GetNodeOrNull<AudioStreamPlayer>(playerName);
		if (player != null)
		{
			return player;
		}

		player = new AudioStreamPlayer
		{
			Name = playerName
		};
		AddChild(player);
		return player;
	}

	private void LoadStreams()
	{
		_bgmPlayer.Stream = LoadOptionalStream(BgmPath);
		_clickPlayer.Stream = LoadOptionalStream(ClickPath);
		_pourPlayer.Stream = LoadOptionalStream(PourPath);
		_blockedPlayer.Stream = LoadOptionalStream(BlockedPath);
		_successPlayer.Stream = LoadOptionalStream(SuccessPath);
	}

	private AudioStream? LoadOptionalStream(string path)
	{
		if (string.IsNullOrWhiteSpace(path))
		{
			return null;
		}

		if (!ResourceLoader.Exists(path))
		{
			WarnMissingPath(path);
			return null;
		}

		AudioStream? stream = GD.Load<AudioStream>(path);
		if (stream == null)
		{
			WarnMissingPath(path);
		}

		return stream;
	}

	private void WarnMissingPath(string path)
	{
		if (_warnedMissingPaths.Add(path))
		{
			GD.PushWarning($"Audio resource missing. AudioManager will no-op for: {path}");
		}
	}

	private void ApplyVolumes()
	{
		_bgmPlayer.VolumeDb = MusicVolumeDb;
		ApplySfxVolume();
	}

	private void ApplySfxVolume()
	{
		float volumeDb = SfxVolumeDb;
		_clickPlayer.VolumeDb = volumeDb;
		_pourPlayer.VolumeDb = volumeDb;
		_blockedPlayer.VolumeDb = volumeDb;
		_successPlayer.VolumeDb = volumeDb;
	}

	private void PlaySfx(AudioStreamPlayer player)
	{
		if (!_isReady || player.Stream == null || _sfxVolume <= 0f)
		{
			return;
		}

		player.Stop();
		player.Play();
	}

	private void OnBgmFinished()
	{
		PlayBgm();
	}

	private int CountPlayersNamed(string playerName)
	{
		int count = 0;
		foreach (Node child in GetChildren())
		{
			if (child is AudioStreamPlayer && child.Name == playerName)
			{
				count++;
			}
		}

		return count;
	}

	private static float VolumeToDb(float value)
	{
		float safeValue = Clamp01(value);
		return safeValue <= 0f ? MuteDb : Mathf.LinearToDb(safeValue);
	}

	private static float Clamp01(float value)
	{
		return float.IsNaN(value) ? 0f : float.Clamp(value, 0f, 1f);
	}
}
