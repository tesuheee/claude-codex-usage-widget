using System;
using System.Globalization;

namespace Headroom
{
    static class UsageParsers
    {
        const long FiveHourWindowSeconds = 5 * 60 * 60;
        const long WeeklyWindowSeconds = 7 * 24 * 60 * 60;

        public static UsageData ParseClaudeApi(string json)
        {
            var data = new UsageData { Name = "Claude", Source = "Claude API", UpdatedAt = DateTime.Now };
            if (string.IsNullOrWhiteSpace(json))
            {
                data.Status = "no_data";
                return data;
            }

            var root = Json.ParseObject(json);
            if (root == null)
            {
                data.Status = "no_data";
                return data;
            }

            var five = Json.Object(root, "five_hour");
            if (five != null)
            {
                data.FiveHourUsed = Json.Double(five, "utilization");
                string reset = Json.String(five, "resets_at");
                if (!string.IsNullOrEmpty(reset)) data.FiveHourReset = ConvertIsoToLegacyFormat(reset);
            }

            var week = Json.Object(root, "seven_day");
            if (week != null)
            {
                data.WeeklyUsed = Json.Double(week, "utilization");
                string reset = Json.String(week, "resets_at");
                if (!string.IsNullOrEmpty(reset)) data.WeeklyReset = ConvertIsoToLegacyFormat(reset);
            }

            if (!data.HasAnyValue()) data.Status = "no_data";
            return data;
        }

        // Extracts the signed-in identity from GET /api/oauth/profile so the card can
        // show which account the token belongs to. Returns null on anything unexpected;
        // the account label is cosmetic and must never break a usage refresh.
        public static string ParseClaudeAccount(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            var root = Json.ParseObject(json);
            if (root == null) return null;

            var account = Json.Object(root, "account");
            if (account == null) return null;

            string email = Json.String(account, "email");
            if (string.IsNullOrWhiteSpace(email)) email = Json.String(account, "display_name");
            return string.IsNullOrWhiteSpace(email) ? null : email;
        }

        public static UsageData ParseCodexApi(string json)
        {
            var data = new UsageData { Name = "Codex", Source = "Codex API", UpdatedAt = DateTime.Now };
            if (string.IsNullOrWhiteSpace(json))
            {
                data.Status = "no_data";
                return data;
            }

            var root = Json.ParseObject(json);
            if (root == null)
            {
                data.Status = "no_data";
                return data;
            }

            var rateLimit = Json.Object(root, "rate_limit");
            var searchRoot = rateLimit ?? root;

            AssignCodexWindow(data, Json.Object(searchRoot, "primary_window"), true);
            AssignCodexWindow(data, Json.Object(searchRoot, "secondary_window"), false);

            if (!data.HasAnyValue()) data.Status = "no_data";
            return data;
        }

        static void AssignCodexWindow(UsageData data, System.Collections.Generic.Dictionary<string, object> window, bool isPrimary)
        {
            if (window == null) return;

            long? duration = Json.Long(window, "limit_window_seconds");
            bool isWeekly = duration == WeeklyWindowSeconds ||
                (duration != FiveHourWindowSeconds && !isPrimary);

            double? used = Json.Double(window, "used_percent");
            long? reset = Json.Long(window, "reset_at");
            string resetText = reset.HasValue && reset.Value > 0
                ? ConvertUnixSecondsToLegacyFormat(reset.Value)
                : null;

            if (isWeekly)
            {
                data.WeeklyUsed = used;
                data.WeeklyReset = resetText;
            }
            else
            {
                data.FiveHourUsed = used;
                data.FiveHourReset = resetText;
            }
        }

        static string ConvertIsoToLegacyFormat(string iso)
        {
            DateTimeOffset dto;
            if (DateTimeOffset.TryParse(iso, CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out dto))
                return dto.ToLocalTime().ToString("yyyy/M/d H:mm", CultureInfo.InvariantCulture);
            return iso;
        }

        static string ConvertUnixSecondsToLegacyFormat(long unixSec)
        {
            try
            {
                var dto = DateTimeOffset.FromUnixTimeSeconds(unixSec);
                return dto.ToLocalTime().ToString("yyyy/M/d H:mm", CultureInfo.InvariantCulture);
            }
            catch { return null; }
        }
    }
}
