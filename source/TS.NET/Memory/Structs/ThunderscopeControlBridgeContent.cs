using System.Runtime.InteropServices;

namespace TS.NET
{
    // Ensure this is blitable (i.e. don't use bool)
    // Pack of 1 = No padding.
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal struct ThunderscopeControlBridgeContent
    {
        internal byte Version;          // Allows UI to know which ThunderscopeControlBridgeHeader version to use, hence the size of the header.

        internal ThunderscopeDataBridgeConfig DataBridge;   // Set by Reader so that Writer knows the max values
        internal ThunderscopeHardwareConfig Hardware;       // Set by Writer, updates are idempotent (so reader needs to resolve changes)
        internal ThunderscopeProcessingConfig Processing;   // Set by Writer, updates are idempotent (so reader needs to resolve changes)
    }
}