#nullable enable

using System;
using System.IO;

namespace ExampleGame;

internal static class Program
{
    private static void Main(string[] args)
    {
        Directory.SetCurrentDirectory(AppContext.BaseDirectory);
        ConfigureLinuxAudioDriver();

        using var game = new ExampleGame();
        game.Run();
    }

    private static void ConfigureLinuxAudioDriver()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        bool isAudioExplicitlyDisabled = string.Equals(
            Environment.GetEnvironmentVariable("HEX_DISABLE_AUDIO"),
            "1",
            StringComparison.Ordinal
        );
        string? configuredAudioDriver = Environment.GetEnvironmentVariable("SDL_AUDIODRIVER");
        if (!string.IsNullOrWhiteSpace(configuredAudioDriver))
        {
            if (string.Equals(configuredAudioDriver, "dummy", StringComparison.OrdinalIgnoreCase))
            {
                Environment.SetEnvironmentVariable("HEX_DISABLE_AUDIO", "1");
            }

            return;
        }

        if (isAudioExplicitlyDisabled)
        {
            Environment.SetEnvironmentVariable("SDL_AUDIODRIVER", "dummy");
            return;
        }

        string? runtimeDirectory = Environment.GetEnvironmentVariable("XDG_RUNTIME_DIR");
        bool hasPipewireSocket =
            !string.IsNullOrWhiteSpace(runtimeDirectory)
            && File.Exists(Path.Combine(runtimeDirectory, "pipewire-0"));
        bool hasPulseSocket =
            !string.IsNullOrWhiteSpace(runtimeDirectory)
            && File.Exists(Path.Combine(runtimeDirectory, "pulse", "native"));
        bool hasAlsaDeviceDirectory = Directory.Exists("/dev/snd");
        string selectedAudioDriver;

        if (!hasAlsaDeviceDirectory)
        {
            selectedAudioDriver = "dummy";
        }
        else if (hasPipewireSocket)
        {
            selectedAudioDriver = "pipewire";
        }
        else if (hasPulseSocket)
        {
            selectedAudioDriver = "pulseaudio";
        }
        else
        {
            selectedAudioDriver = "alsa";
        }

        Environment.SetEnvironmentVariable("SDL_AUDIODRIVER", selectedAudioDriver);
    }
}
