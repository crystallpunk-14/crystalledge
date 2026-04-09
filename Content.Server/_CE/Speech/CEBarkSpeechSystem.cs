using System.Text.RegularExpressions;
using Content.Shared._CE.Speech;
using Content.Shared.Chat;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._CE.Speech;

/// <summary>
/// Produces Animal Crossing-style "bark" speech by playing a rapid sequence of
/// pitched syllable sounds when an entity speaks. Sound selection is per-syllable
/// based on proximity to sentence-ending punctuation.
/// </summary>
public sealed class CEBarkSpeechSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;

    private static readonly Regex MarkupRegex = new(@"\[.*?\]", RegexOptions.Compiled);

    /// <summary>
    /// Active bark sequences currently playing per entity.
    /// </summary>
    private readonly Dictionary<EntityUid, BarkSequence> _activeBarks = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CEBarkSpeechComponent, EntitySpokeEvent>(OnEntitySpoke);
    }

    private void OnEntitySpoke(EntityUid uid, CEBarkSpeechComponent comp, EntitySpokeEvent args)
    {
        var message = StripMarkup(args.Message);
        if (string.IsNullOrWhiteSpace(message))
            return;

        if (!_proto.TryIndex(comp.BarkSpeech, out var profile))
            return;

        var syllables = BuildSyllables(message, comp.BasePitch, profile);
        if (syllables.Count == 0)
            return;

        _activeBarks[uid] = new BarkSequence
        {
            Syllables = syllables,
            NextIndex = 0,
            NextPlayTime = _timing.CurTime,
        };
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_activeBarks.Count == 0)
            return;

        var now = _timing.CurTime;
        var toRemove = new List<EntityUid>();

        foreach (var (uid, seq) in _activeBarks)
        {
            if (!Exists(uid))
            {
                toRemove.Add(uid);
                continue;
            }

            while (seq.NextIndex < seq.Syllables.Count && now >= seq.NextPlayTime)
            {
                var syllable = seq.Syllables[seq.NextIndex];

                if (!syllable.IsPause)
                {
                    var audioParams = syllable.AudioParams
                        .WithPitchScale(syllable.Pitch)
                        .WithVolume(syllable.AudioParams.Volume + syllable.VolumeBoost);

                    _audio.PlayPvs(syllable.Sound, uid, audioParams);
                }

                seq.NextIndex++;

                if (seq.NextIndex < seq.Syllables.Count)
                    seq.NextPlayTime = now + TimeSpan.FromSeconds(syllable.Duration);
            }

            if (seq.NextIndex >= seq.Syllables.Count)
                toRemove.Add(uid);
        }

        foreach (var uid in toRemove)
        {
            _activeBarks.Remove(uid);
        }
    }

    /// <summary>
    /// Splits message into sentences, then builds syllables per sentence with
    /// per-syllable sound selection based on proximity to sentence punctuation.
    /// </summary>
    private List<BarkSyllable> BuildSyllables(string message, float basePitch, CEBarkSpeechPrototype profile)
    {
        var result = new List<BarkSyllable>();
        var sentences = SplitSentences(message);
        var totalSyllables = 0;

        foreach (var sentence in sentences)
        {
            if (totalSyllables >= profile.MaxSyllables)
                break;

            var intonation = DetectIntonation(sentence);
            var text = sentence.TrimEnd('.', '!', '?', ' ');
            if (string.IsNullOrEmpty(text))
                continue;

            // Build syllables for this sentence.
            var sentenceSyllables = new List<BarkSyllable>();
            var charIndex = 0;

            while (charIndex < text.Length && totalSyllables < profile.MaxSyllables)
            {
                var ch = text[charIndex];

                if (ch == ' ')
                {
                    sentenceSyllables.Add(new BarkSyllable
                    {
                        IsPause = true,
                        Duration = profile.SyllableInterval * profile.WordPauseMultiplier,
                    });
                    charIndex++;
                    continue;
                }

                if (ch == ',')
                {
                    sentenceSyllables.Add(new BarkSyllable
                    {
                        IsPause = true,
                        Duration = profile.SyllableInterval * profile.CommaPauseMultiplier,
                    });
                    charIndex++;
                    continue;
                }

                if (!char.IsLetterOrDigit(ch))
                {
                    charIndex++;
                    continue;
                }

                var consumed = 0;
                var representativeChar = ch;
                while (consumed < profile.CharsPerSyllable && charIndex < text.Length)
                {
                    var c = text[charIndex];
                    if (c == ' ' || c == ',')
                        break;

                    if (char.IsLetterOrDigit(c))
                    {
                        if (consumed == 0)
                            representativeChar = c;
                        consumed++;
                    }

                    charIndex++;
                }

                var pitch = CalculatePitch(representativeChar, basePitch, profile);

                sentenceSyllables.Add(new BarkSyllable
                {
                    Pitch = pitch,
                    Duration = profile.SyllableInterval,
                });

                totalSyllables++;
            }

            // Assign sounds and intonation modifiers per syllable.
            // Count non-pause syllables to calculate position within sentence.
            var voicedCount = 0;
            foreach (var s in sentenceSyllables)
            {
                if (!s.IsPause)
                    voicedCount++;
            }

            var voicedIndex = 0;
            foreach (var s in sentenceSyllables)
            {
                if (s.IsPause)
                {
                    result.Add(s);
                    continue;
                }

                var position = voicedCount > 1 ? (float) voicedIndex / (voicedCount - 1) : 0f;
                var modified = s;

                // Select sound based on position in sentence and intonation.
                modified.Sound = SelectSound(position, intonation, profile);
                modified.AudioParams = profile.AudioParams;

                // Apply intonation pitch/volume modifiers.
                modified.Pitch = ApplyIntonation(modified.Pitch, position, intonation, profile);

                if (intonation == Intonation.Exclaim)
                    modified.VolumeBoost = profile.ExclaimVolumeBoost;

                result.Add(modified);
                voicedIndex++;
            }

            // Sentence boundary pause.
            if (result.Count > 0)
            {
                result.Add(new BarkSyllable
                {
                    IsPause = true,
                    Duration = profile.SyllableInterval * profile.CommaPauseMultiplier,
                });
            }
        }

        return result;
    }

    /// <summary>
    /// Selects which sound to play for a syllable based on its position within
    /// the sentence and the sentence's intonation.
    /// Syllables in the last 30% of a question/exclamation sentence use the
    /// corresponding intonation sound; the rest use the default say sound.
    /// </summary>
    private static SoundSpecifier SelectSound(float position, Intonation intonation, CEBarkSpeechPrototype profile)
    {
        if (position >= 0.7f)
        {
            return intonation switch
            {
                Intonation.Question => profile.AskSound ?? profile.SaySound,
                Intonation.Exclaim => profile.ExclaimSound ?? profile.SaySound,
                _ => profile.SaySound,
            };
        }

        return profile.SaySound;
    }

    private float CalculatePitch(char ch, float basePitch, CEBarkSpeechPrototype profile)
    {
        ch = char.ToLowerInvariant(ch);

        float charFactor;
        if ("aeiouаеёиоуыэюя".Contains(ch))
        {
            charFactor = ch switch
            {
                'a' or 'а' => 0.0f,
                'o' or 'о' => 0.1f,
                'u' or 'у' => 0.15f,
                'e' or 'е' or 'э' => 0.25f,
                'i' or 'и' => 0.3f,
                'ы' => 0.05f,
                'ё' => 0.2f,
                'ю' => 0.12f,
                'я' => 0.28f,
                _ => 0.15f,
            };
        }
        else if (char.IsDigit(ch))
        {
            charFactor = (ch - '0') * 0.04f + 0.1f;
        }
        else
        {
            charFactor = ((ch - 'a' + 16) % 26) * 0.015f + 0.35f;
        }

        var pitchOffset = (charFactor - 0.25f) * 0.4f;
        var variation = (float) _random.NextGaussian(0, profile.PitchVariation);

        return basePitch + pitchOffset + variation;
    }

    private static float ApplyIntonation(
        float pitch,
        float position,
        Intonation intonation,
        CEBarkSpeechPrototype profile)
    {
        switch (intonation)
        {
            case Intonation.Question:
                if (position > 0.7f)
                {
                    var rise = (position - 0.7f) / 0.3f;
                    pitch += profile.QuestionPitchRise * rise;
                }
                break;

            case Intonation.Exclaim:
                pitch += profile.ExclaimPitchBoost;
                break;

            case Intonation.Declarative:
                if (position > 0.8f)
                {
                    var drop = (position - 0.8f) / 0.2f;
                    pitch -= profile.DeclarativePitchDrop * drop;
                }
                break;
        }

        return pitch;
    }

    /// <summary>
    /// Splits message into sentences at punctuation boundaries.
    /// </summary>
    private static List<string> SplitSentences(string message)
    {
        var sentences = new List<string>();
        var start = 0;

        for (var i = 0; i < message.Length; i++)
        {
            if (message[i] is '.' or '!' or '?')
            {
                sentences.Add(message[start..(i + 1)]);
                start = i + 1;
            }
        }

        if (start < message.Length)
            sentences.Add(message[start..]);

        return sentences;
    }

    private static Intonation DetectIntonation(string sentence)
    {
        var trimmed = sentence.TrimEnd();
        if (trimmed.Length == 0)
            return Intonation.Declarative;

        return trimmed[^1] switch
        {
            '?' => Intonation.Question,
            '!' => Intonation.Exclaim,
            _ => Intonation.Declarative,
        };
    }

    private static string StripMarkup(string message)
    {
        return MarkupRegex.Replace(message, string.Empty);
    }

    private sealed class BarkSequence
    {
        public List<BarkSyllable> Syllables = new();
        public int NextIndex;
        public TimeSpan NextPlayTime;
    }

    private struct BarkSyllable
    {
        public bool IsPause;
        public float Pitch;
        public float Duration;
        public float VolumeBoost;
        public SoundSpecifier Sound;
        public AudioParams AudioParams;
    }

    private enum Intonation : byte
    {
        Declarative,
        Question,
        Exclaim,
    }
}
