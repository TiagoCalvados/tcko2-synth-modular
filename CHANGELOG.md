# Changelog

## v0.14.1

- Fixes Glitch, Sound Technologies and Old Electro menus reverting to their current selection when clicking another item. Live refresh now leaves open dropdown menus alone and avoids rewriting unchanged selections.
- Adds a native menu regression check that detects the original selection-reset messages, verifies every choice, and keeps earlier layers and held notes running. Layering and core DSP/MIDI/UI checks pass.

## v0.14.0

- Main presets, Sound Technologies and Old Electro now build one keyboard mix. Earlier sounds remain even when another sound is selected in the same box; newly added sounds join held notes immediately.
- Glitch and Fragments process the actual combined waveform. Removes independent fixed-note Glitch/Old Electro transport controls.
- Adds a Layers selector and Remove, per-selected Technology/Old Electro mute and level, and complete silent stack restoration.
- Supports eight layers within a shared sixteen-voice held-note budget. Extra layers/notes are refused without replacing existing sounds. Uses short gain ramps and shared prepared technology tables.
- Tests shared computer/on-screen/MIDI input, chords, sustain, removal, recording, mix effects, session settings, master, fade and render capacity. Matching Project Stories version adds visible removable layers and mobile capacity handling.

## v0.13.2

- Adds Sound Technologies between smaller Glitch and Old Electro boxes, with twelve original procedural keyboard instruments, short names, descriptions, Character and Volume.
- Selecting a technology is silent. On-screen keys, computer keys and MIDI play its notes and chords; note-off, sustain, pitch/speed, effects, Master, Fade, Panic and REC use the existing keyboard engine.
- Implements granular, waveguide, frozen-spectrum, modal, friction, bubble, folding, frequency-shift, convolution, stochastic, spatial and spectral-morph signal paths. Documents their model scope and research basis.
- Verifies twelve distinct signals, matching native/browser synthesis, keyboard input routes, chords, note release, recording, silent selection and restored settings. Browser Keep playing layers retain their existing behaviour.

## v0.12.0

- Open sound beside the title loads local WAV, M4A/AAC, MP3 and MP4 audio from Projects/TCK02/SOUNDS. Play and Stop sound control the file independently; master, Fade, Panic and performed REC include it.
- The upper selector keeps eleven original sounds. All eight additional patches remain in Old Electro.
- Publishes the current desktop features: Glitch types and volume, Fragments, pitch/speed, note clarity, persistent settings, recording, and independent Glitch/Old Electro boxes.
- Core verification no longer opens live audio or MIDI devices. New file tests cover importing, cancellation, errors, recording and transport.

## v0.3.0


First GitHub release as **TCKO2 Synth modular**.

- Standalone Windows x64 instrument with USB MIDI, audio output selection and fallback keyboards.
- Eleven presets and eight MIDI-learn sound controls.
- Sustain and drone latches, Bloom, expansive stereo textures and long natural releases.
- Echo spacing up to four seconds, longer feedback tails and larger reverb.
- Volume, Stop/Resume and Panic controls.
- Built-in DSP, MIDI, UI and Windows audio verification.
