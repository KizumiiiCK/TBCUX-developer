using System;
using System.Collections.Generic;

/// <summary>
/// Idempotent claim log for Builda <c>pay.showPayPanel</c> order ids.
/// The host may return the same <c>orderId</c> again; the game must not grant twice.
/// </summary>
public static class BuildaPayOrders
{
    /// <summary>
    /// Returns true the first time <paramref name="orderId"/> is seen and records it.
    /// Returns false when it was already claimed (or the id is empty).
    /// </summary>
    public static bool TryClaim(string orderId)
    {
        if (string.IsNullOrEmpty(orderId)) return false;

        string[] old = SaveCodec.DecodeStringArray(BuildaSaveBackend.Get(SaveKeys.PayOrders));
        var list = old != null ? new List<string>(old) : new List<string>();
        for (int i = 0; i < list.Count; i++)
        {
            if (string.Equals(list[i], orderId, StringComparison.Ordinal)) return false;
        }

        list.Add(orderId);
        BuildaSaveBackend.Set(SaveKeys.PayOrders, SaveCodec.EncodeStringArray(list.ToArray()));
        return true;
    }
}
