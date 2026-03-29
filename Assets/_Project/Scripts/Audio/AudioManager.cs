using UnityEngine;
using Bluff.Core;

/// <summary>
/// Central audio hub. Assign clips in the Inspector on the AudioManager prefab/GameObject.
/// All methods are null-safe — nothing breaks if clips aren't assigned yet.
/// </summary>
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("SFX — Cards")]
    [SerializeField] private AudioClip _clipCardClick;
    [SerializeField] private AudioClip _clipCardDeal;

    [Header("SFX — Actions")]
    [SerializeField] private AudioClip _clipBetPlaced;
    [SerializeField] private AudioClip _clipBelieveCorrect;
    [SerializeField] private AudioClip _clipBelieveWrong;
    [SerializeField] private AudioClip _clipBluffCaught;
    [SerializeField] private AudioClip _clipBluffWrong;

    [Header("SFX — Game Events")]
    [SerializeField] private AudioClip _clipYourTurn;
    [SerializeField] private AudioClip _clipCountdownTick;
    [SerializeField] private AudioClip _clipTimerWarning;
    [SerializeField] private AudioClip _clipGameWin;
    [SerializeField] private AudioClip _clipGameLose;

    [Header("Music")]
    [SerializeField] private AudioClip _clipMenuMusic;
    [SerializeField] private AudioClip _clipGameMusic;

    [Header("Volume")]
    [Range(0f, 1f)] [SerializeField] private float _sfxVolume  = 1f;
    [Range(0f, 1f)] [SerializeField] private float _musicVolume = 0.35f;

    private AudioSource _sfxSource;
    private AudioSource _musicSource;

    private bool _muted;

    // Tracks whose turn it was last refresh to fire YourTurn only on change
    private string _lastCurrentPlayerId = "";

    public static bool IsMuted => Instance != null && Instance._muted;

    // ── UNITY ────────────────────────────────────────────────

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        _sfxSource   = gameObject.AddComponent<AudioSource>();
        _sfxSource.playOnAwake = false;

        _musicSource = gameObject.AddComponent<AudioSource>();
        _musicSource.playOnAwake = false;
        _musicSource.loop        = true;
        _musicSource.volume      = _musicVolume;

        _muted = PlayerPrefs.GetInt("bluff_muted", 0) == 1;
        ApplyMute();
    }

    private void ApplyMute()
    {
        if (_sfxSource   != null) _sfxSource.mute   = _muted;
        if (_musicSource != null) _musicSource.mute  = _muted;
    }

    void Start()
    {
        NetworkedGameManager.OnGameStarted       += OnGameStarted;
        NetworkedGameManager.OnStateRefresh      += OnStateRefresh;
        NetworkedGameManager.OnCardRevealed      += OnCardRevealed;
        NetworkedGameManager.OnGameOver          += OnGameOver;
        NetworkedGameManager.OnCountdownTick     += OnCountdownTick;
        NetworkedGameManager.OnGameResetting     += OnGameResetting;
        NetworkedGameManager.OnTurnTimedOut      += OnTurnTimedOut;
        NetworkedGameManager.OnGuessingStarted   += OnGuessingStarted;
        NetworkedGameManager.OnPlayerReconnected += OnPlayerReconnected;

        // Lobby music on start
        PlayMusic(_clipMenuMusic);
    }

    void OnDestroy()
    {
        NetworkedGameManager.OnGameStarted       -= OnGameStarted;
        NetworkedGameManager.OnStateRefresh      -= OnStateRefresh;
        NetworkedGameManager.OnCardRevealed      -= OnCardRevealed;
        NetworkedGameManager.OnGameOver          -= OnGameOver;
        NetworkedGameManager.OnCountdownTick     -= OnCountdownTick;
        NetworkedGameManager.OnGameResetting     -= OnGameResetting;
        NetworkedGameManager.OnTurnTimedOut      -= OnTurnTimedOut;
        NetworkedGameManager.OnGuessingStarted   -= OnGuessingStarted;
        NetworkedGameManager.OnPlayerReconnected -= OnPlayerReconnected;
    }

    // ── EVENT HANDLERS ────────────────────────────────────────

    private void OnGameStarted()
    {
        _lastCurrentPlayerId = "";
        PlayMusic(_clipGameMusic);
        Play(_clipCardDeal);
    }

    private void OnStateRefresh(GameState state, string localId)
    {
        string current = state.CurrentPlayer?.Id ?? "";
        if (current == localId && current != _lastCurrentPlayerId)
            Play(_clipYourTurn);
        _lastCurrentPlayerId = current;
    }

    private void OnCardRevealed(Card card, string revealerName, bool wasCorrect,
        string action, string declaredRank, int _)
    {
        if (action == "Believe")
            Play(wasCorrect ? _clipBelieveCorrect : _clipBelieveWrong);
        else if (action == "Bluff")
            Play(wasCorrect ? _clipBluffCaught : _clipBluffWrong);
    }

    private void OnGameOver(string loserName)
    {
        // Determine if local player is the loser
        var ngm  = NetworkedGameManager.Instance;
        bool isLocalLoser = ngm != null &&
            ngm.GetLocalState()?.Loser != null &&
            ngm.GetLocalState().Players.Count > 0 &&
            ngm.GetLocalState().Loser.Name == loserName;

        Play(isLocalLoser ? _clipGameLose : _clipGameWin);
        _musicSource.Stop();
    }

    private void OnGameResetting() => PlayMusic(_clipMenuMusic);

    private void OnTurnTimedOut(string _) => Play(_clipTimerWarning);

    private void OnGuessingStarted(string guesserName, string action,
        string declaredRank, int cardCount, bool isLocalGuesser)
    {
        // Play the card-deal sound as a "reveal is about to happen" cue
        Play(_clipCardDeal);
    }

    private void OnPlayerReconnected(string playerName)
    {
        Play(_clipYourTurn);
    }

    private void OnCountdownTick(int seconds)
    {
        if (seconds > 0) Play(_clipCountdownTick);
    }

    // ── PUBLIC STATIC HELPERS ────────────────────────────────

    public static void PlayCardClick() => Instance?.Play(Instance._clipCardClick);
    public static void PlayBetPlaced() => Instance?.Play(Instance._clipBetPlaced);
    public static void PlayCardRevealed(bool wasCorrect, string action)
    {
        if (Instance == null) return;
        if (action == "Believe")
            Instance.Play(wasCorrect ? Instance._clipBelieveCorrect : Instance._clipBelieveWrong);
        else if (action == "Bluff")
            Instance.Play(wasCorrect ? Instance._clipBluffCaught : Instance._clipBluffWrong);
    }

    public static void ToggleMute()
    {
        if (Instance == null) return;
        Instance._muted = !Instance._muted;
        Instance.ApplyMute();
        PlayerPrefs.SetInt("bluff_muted", Instance._muted ? 1 : 0);
        PlayerPrefs.Save();
    }

    // ── INTERNALS ────────────────────────────────────────────

    private void Play(AudioClip clip)
    {
        if (clip == null || _sfxSource == null) return;
        _sfxSource.PlayOneShot(clip, _sfxVolume);
    }

    private void PlayMusic(AudioClip clip)
    {
        if (clip == null) { _musicSource?.Stop(); return; }
        if (_musicSource.clip == clip && _musicSource.isPlaying) return;
        _musicSource.clip = clip;
        _musicSource.Play();
    }
}
