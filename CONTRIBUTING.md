# Contributing

Thanks for considering a contribution to VOID Overlay Metron.

## Before submitting

- Keep the app local-only; do not add telemetry or upload machine data.
- Never inject into games, read game memory, or bypass anti-cheat protections.
- Do not label refresh-rate or overlay-loop measurements as game FPS.
- Keep unavailable metrics visibly unavailable rather than displaying guesses.
- Prefer small changes and add tests for logic that can run without specialized hardware.

## Development

1. Install the .NET 10 SDK on Windows.
2. Run `.\scripts\build.ps1`.
3. Run `.\scripts\test.ps1`.
4. For framework-dependent Windows x64 output, run `.\scripts\publish.ps1`.

Please describe Windows version, GPU-counter availability, and reproducible steps for issues. Avoid attaching full logs unless requested; redact usernames and other personal information.

## Pull requests

Explain the user-visible effect, relevant limitations, and tests performed. Do not add a dependency without checking its maintenance status, license, and runtime impact.
