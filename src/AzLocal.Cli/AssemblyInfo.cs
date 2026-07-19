using System.Runtime.CompilerServices;

// Exposes internal, side-effect-free helpers (port validation, process-info building) to unit
// tests without making them part of the CLI's public surface.
[assembly: InternalsVisibleTo("AzLocal.UnitTests")]
