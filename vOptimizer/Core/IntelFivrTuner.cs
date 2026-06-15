using System;

namespace vOptimizer.Core
{
    /// <summary>
    /// Handles undervolting and voltage offsets for Intel Core processors via MSR 0x150 (FIVR Control).
    /// </summary>
    public static class IntelFivrTuner
    {
        private const uint MSR_FIVR_CONTROL = 0x150;

        // Intel FIVR Target Planes
        public const int PlaneCpuCore = 0;
        public const int PlaneiGpu = 1;
        public const int PlaneCpuCache = 2; // Also known as Uncore / Ring
        public const int PlaneSystemAgent = 3;
        public const int PlaneAnalogIo = 4;

        /// <summary>
        /// Applies an undervolt offset (in millivolts) to the specified plane.
        /// </summary>
        /// <param name="plane">The target plane (0=Core, 2=Cache, etc.)</param>
        /// <param name="offsetMv">The negative voltage offset in millivolts (e.g., -80 for -80mV)</param>
        /// <returns>True if the write command was sent successfully via WinRing0</returns>
        public static bool ApplyUndervolt(int plane, int offsetMv)
        {
            try
            {
                // FIVR uses voltage units of 1/1024 Volt (~0.976 mV).
                // Formula: Units = Offset (mV) * 1.024
                int units = (int)Math.Round(offsetMv * 1.024);

                // Convert to a 12-bit signed integer (masked to 12 bits for safety)
                uint offsetValue = (uint)(units & 0xFFF);

                // Build the 32-bit FIVR Command Mailbox:
                // Bit 31: 1 = Write Enable
                // Bit 30-28: 0 = FIVR command type (voltage)
                // Bit 27-24: Target Plane ID
                // Bit 23-21: Reserved (0)
                // Bit 20-8: Offset Value (12-bit signed value)
                // Bit 7-0: Mode (0 = Adaptive, 1 = Override)
                uint command = 0x80000000;             // Write enabled
                command |= (uint)(plane << 24);         // Target Plane ID
                command |= (uint)(offsetValue << 8);     // Offset value in bits 20-8
                command |= 0;                           // Mode 0 = Adaptive (retains stock behavior at idle)

                // Write to MSR 0x150. EAX is the low 32 bits (our command), EDX is the high 32 bits (0 for offsets)
                return WinRing0.Wrmsr(MSR_FIVR_CONTROL, command, 0);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Reads the current voltage offset (in millivolts) for the specified plane.
        /// </summary>
        public static double GetCurrentOffset(int plane)
        {
            try
            {
                // Write Read Command to FIVR Mailbox (Bit 31 = 0)
                uint command = 0x00000000; // Read (Bit 31 = 0)
                command |= (uint)(plane << 24);

                // Send read request
                WinRing0.Wrmsr(MSR_FIVR_CONTROL, command, 0);

                // Read the output value
                if (WinRing0.Rdmsr(MSR_FIVR_CONTROL, out uint eax, out uint edx))
                {
                    // Bits 20-8 contain the read offset in 1/1024V units
                    uint offsetBits = (eax >> 8) & 0xFFF;
                    
                    // Sign extend 12-bit signed integer to 32-bit signed integer
                    int units = (int)offsetBits;
                    if ((units & 0x800) != 0) // If sign bit (bit 11) is 1
                    {
                        units |= ~0xFFF; // Fill sign bits
                    }

                    // Convert back to mV: mV = Units / 1.024
                    return Math.Round(units / 1.024, 1);
                }
            }
            catch { }
            return 0.0;
        }
    }
}
