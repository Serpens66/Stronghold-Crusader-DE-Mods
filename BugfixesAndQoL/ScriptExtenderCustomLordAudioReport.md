# WAV decoder unnecessarily rejects 48 kHz Custom Lord speech

## Current behavior

`AudioClipExtensions.LoadWavFromBytes` rejects every sample rate except 44100 Hz:

    if (sampleRate != 44100)
        throw new NotSupportedException(...);

Legacy Custom Lord media can contain valid 16-bit PCM mono WAV files at 48000 Hz. The Asset API finds these files, but playback remains silent because this check throws. The OGG path already creates its `AudioClip` with the source sample rate.

## Suggested fix

Remove the exact-44100 check, or replace it with a documented reasonable Unity-supported range. The existing call already passes `sampleRate` to `AudioClip.Create`, so no resampling is required for valid PCM WAV input.
