# TCKO2 Synth modular

A small, standalone Windows synthesizer. Double-click **TCKO2Synth.exe** or **Start TCKO2 Synth modular.cmd**. No account, music software, downloads, or installer are needed on this PC. Keep the executable in a writable folder if you want MIDI knob assignments to survive closing it.

## First play

1. Connect the Akai MPK mini Play mk3 to the PC by USB. Set its rear power switch to **USB**, and press **Internal Sounds** so that button is off.
2. TCKO2 Synth modular should show **CONNECTED · MPK mini Play mk3**. If needed, click **Rescan MIDI**, or select the device in the MIDI menu. The computer/on-screen keyboard works without MIDI.
3. Use headphones or speakers connected to your **PC**. The Akai-to-bass-amp cable carries the Akai's internal sound; TCKO2 Synth modular's generated audio comes from the PC. USB supplies MIDI control, not this synth's audio to the Akai.
4. Start with low PC speaker volume. Master starts at 20%. Play a few keys, select a preset, and explore the dials. If necessary choose a PC output from the audio menu. **Restart audio** reopens it after an audio device change.

For a DJ mixer setup: **Akai USB → Windows PC → PC headphone/line output (or PC USB audio interface) → mixer LINE input → speakers**. The PC must run the synth and send its audio to the mixer. Connecting only the Akai's headphone output to the mixer plays its built-in sounds.


## Eleven starting points

| Preset | Try this |
| --- | --- |
| Velvet Voltage | Hold a chord; increase Drift and Room for a warm, wandering pad. |
| Cellar Acid | Play single low notes; increase Squelch and sweep Tone for rubbery filter movement. |
| Dusty Polaroid | Play short chords; add Tape echo and Motion for vintage keys. |
| Copper Satellites | Explore Metal for inharmonic bells and radio-like textures. |
| Ghost Observatory | Hold low notes; slowly move Tone and Motion for a breathing sci-fi drone. |
| Below the Floor / Drone | A deep, warm drone. Tap a low note once and sweep Tone slowly. |
| Lunar Array / Drone | A metallic space drone. Stack a few notes and explore Metal. |
| Rust Cathedral / Drone | A dense, resonant drone. Add a fifth and swell the reverb. |
| Broken Transistor Organ | An immediate vintage organ. Use Sustain latch for lasting chords. |
| Soot Ribbon Lead | A gritty lead with filter movement and tape echo. |
| Arcade Vapour | Hollow, shimmering keys with a glassy ring-modulation edge. |

The eight dials control filter cutoff, resonance, oscillator drift/detuning, saturation, tape-style echo amount/feedback, reverb amount/decay, ring modulation amount/ratio, and LFO movement. Drag up/down, use the mouse wheel, or focus a dial and use arrow keys. Parameters glide to reduce abrupt changes. Preset changes clear held notes and effect tails.

## Sustain and drone

**SUSTAIN LATCH** acts like a sustain pedal you can leave on: play notes or chords, release the keys, and they stay held. **Bloom** raises softer sustain levels and adds fullness and space. Turn the latch off to release the notes with a natural fade. If a real MIDI sustain pedal is still down, those notes continue until you release the pedal too.

**DRONE LATCH** holds each note continuously. Tap a key once to add it to your drone chord; tap the same key again to release it. It keeps a strong sustain level even with a short bell/keys preset. Turn the checkbox off to release the chord. Notes use their preset release time, so reverb and echo may fade afterwards. It supports up to eight voices; adding a ninth replaces the oldest.

The three presets labelled **/ Drone** switch Drone latch on automatically when selected. Start with C3 on the on-screen keyboard or a low Akai note. Add other notes, then shape Tone, Drift, Metal and Motion while they play. The two latch modes are alternatives: switching one on switches the other off. Intentionally latched notes continue if the app loses focus. Normal computer/mouse keys are released on focus loss.

**PANIC / Esc** and **STOP** clear every note, both latch modes, and all effect tails. Changing a preset also clears the old notes; only a dedicated drone preset enables its Drone latch again. The app starts with both latches off and no note playing.

## Bloom and long echo

**BLOOM** controls the expansiveness of sustained/drone notes: two additional detuned oscillators, a slow stereo chorus, wider drift and a fuller sustain level. Held notes retain their expanded texture as they fade. At high Bloom, their release lasts roughly 12–14 seconds; the dedicated drone presets have slow 2–3.5 second swells and 12–16 second releases. Bloom at zero gives a simpler, tighter texture. MIDI pedal-held notes also respond to Bloom.

**ECHO TIME** controls the spacing of repeats from **0.08 to 4.00 seconds**. **Tape echo** remains the amount/feedback dial: raise it for more repeats and a much longer tail. Changing time glides the delay, producing tape-like pitch movement. The stereo repeats cross-feed with slightly different left/right timing. Reverb now has larger delay chambers and a longer decay too.

For a spacious starting point, select **Lunar Array / Drone** (90% Bloom, 2.2-second echo spacing) or **Rust Cathedral / Drone**, tap a low note and a fifth, and shape Tone slowly. Lower Tape echo or Room for a clearer sound. Panic clears even the long tails immediately apart from the short Windows output buffer.

## Teach the Akai knobs

Click **LEARN** on a dial, then turn the physical knob you want to assign. The card will display its CC number and MIDI channel. Click LEARN again to cancel, or press Esc. Assignments persist in **knob-mappings.txt** beside the app and work across presets. Assigning an already-used knob moves its assignment to the new dial.

The Play mk3 has four knobs with **Bank A/B**, giving eight possible assignments. Learn four in bank A, switch to B, and learn the other four. If a knob does not produce an event, make sure Internal Sounds is off and check the activity display. Some controller configurations reuse CCs; the app displays the actual received values instead of assuming factory assignments. Sustain CC64 and MIDI sound-off/reset messages retain their normal functions and are reserved from learning.

## Other controls

- **Master** controls this synth's output. The Akai volume knob controls its internal audio, not TCKO2 Synth modular's master.
- **STOP / RESUME** mutes the engine, clears notes/tails, and ignores new notes until resumed.
- **PANIC / Esc** immediately clears notes, sustain, pitch bend, and effect buffers. Further notes can play normally. Windows may have a short already-queued audio tail.
- **Sound settings** opens Windows output settings. **Restart audio** refreshes available outputs and tries the selected device again.
- Computer keys **A W S E D F T G Y H U J K O L P ;** play chromatically from C3. **Z / X** move the octave. Keyboard layout differences can affect punctuation keys.
- The on-screen keyboard supports click/drag notes. Losing app focus releases ordinary computer/mouse notes. Notes held by Sustain or Drone latch continue; MIDI continues working in the background.
- Velocity, a sustain pedal, and pitch bend (fixed to ±2 semitones) are supported. Pads play synth notes on whichever channel they transmit; this prototype has no separate drum engine.

## What this prototype contains

Eight voices, band-limited saw/pulse oscillators (two oscillators per voice, expanding to four with Bloom), a sine sub oscillator, drifting detuning, stereo chorus, a resonant state-variable low-pass filter with envelope movement, soft saturation, long stereo cross-feedback echo with filtered repeats and wow, stereo comb reverb, and ring modulation. A DC blocker and soft output ceiling bound the output. These are analogue-inspired digital effects; there is no sampled vintage hardware or external plug-in.

Audio uses built-in Windows WinMM, 44.1 kHz / 16-bit stereo with three 256-frame buffers (about 17 ms of app buffering, plus Windows/device latency and MIDI polling). This is a first-play instrument rather than a DAW. If you hear crackles, close CPU-heavy apps; Bluetooth and some Windows audio routes add delay. Select wired PC speakers/headphones for the first play check.

## Building and verification

See the repository [README](../README.md) for source build and test instructions.

## Reference documentation

- [Akai Play mk3 manual](https://cdn.inmusicbrands.com/akai/mpk-mini-play-mk3/MPK%20mini%20Play%20mk3%20-%20User%20Guide%20-%20v1.1.pdf): USB setup, Internal Sounds and knob banks.
- [Microsoft MIDI input](https://learn.microsoft.com/en-us/windows/win32/api/mmeapi/nf-mmeapi-midiinopen) and [Windows audio output](https://learn.microsoft.com/en-us/windows/win32/api/mmeapi/nf-mmeapi-waveoutopen): native device interfaces.
