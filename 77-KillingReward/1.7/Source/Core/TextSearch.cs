using System;

namespace KillingReward.Core
{
    public static class TextSearch
    {
        public static bool Matches(string label, string query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return true;
            }
            return label != null && label.IndexOf(query.Trim(), StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
