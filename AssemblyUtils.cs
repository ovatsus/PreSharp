using System;
using System.IO;
using System.Reflection;

internal static class AssemblyUtils {

    /// <summary>
    /// Copies the current assembly to the destination, if necessary.
    /// </summary>
    /// <param name="destination">Destination path. Includes filename.</param>
    /// <returns>False if the copy was skipped</returns>
    public static bool CopyTo(string destination) {
        if (!Assembly.GetExecutingAssembly().Location.Equals(destination, StringComparison.OrdinalIgnoreCase)) {
            if (!File.Exists(destination) || HasAssemblyChanged(destination)) {
                File.Copy(Assembly.GetExecutingAssembly().Location, destination, true);
                return true;
            }
        }
        return false;
    }

    public static bool HasAssemblyChanged(string otherAssemblyPath) {

        try {
            byte[] existing = File.ReadAllBytes(otherAssemblyPath);
            byte[] current = File.ReadAllBytes(Assembly.GetExecutingAssembly().Location);
            if (existing.Length != current.Length) {
                return true;
            }

            for (int i = 0; i < existing.Length; i++) {
                if (existing[i] != current[i]) {
                    return true;
                }
            }
            return false;
        } catch {
            return true;
        }
    }

    public static Version GetVersion() {
        return Assembly.GetExecutingAssembly().GetName().Version;
    }

    public static Version GetCLRVersion() {
        return GetCLRVersion(typeof(AssemblyUtils));
    }

    public static Version GetCLRVersion(Type type) {
        return new Version(Assembly.GetAssembly(type).ImageRuntimeVersion.TrimStart('v'));
    }

    public static bool IsRunningAt32Bit() {
        //easiest most performant way to detect
        return IntPtr.Size == 4;
    }
}
