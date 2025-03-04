﻿using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Content.Shared.CCVar;
using Robust.Shared.Configuration;
using Robust.Shared.Network;
using Robust.Shared.Utility;

namespace Content.Server.Corvax.Sponsors;

public sealed class SponsorsManager
{
    [Dependency] private readonly IServerNetManager _netMgr = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;

    private readonly HttpClient _httpClient = new();

    private ISawmill _sawmill = default!;
    private string _apiUrl = string.Empty;

    private readonly Dictionary<NetUserId, SponsorInfo> _cachedSponsors = new();

    public void Initialize()
    {
        _sawmill = Logger.GetSawmill("sponsors");
        _cfg.OnValueChanged(CCVars.SponsorsApiUrl, s => _apiUrl = s, true);
        _netMgr.Connecting += OnConnecting;
        _netMgr.Disconnect += OnDisconnect;
    }

    public SponsorInfo? GetSponsorInfo(NetUserId userId)
    {
        if (!_cachedSponsors.TryGetValue(userId, out var sponsor))
            return null;
        return sponsor;
    }

    private async Task OnConnecting(NetConnectingArgs e)
    {
        var info = await LoadSponsorInfo(e.UserId);
        if (info?.Tier == null)
            return;

        DebugTools.Assert(!_cachedSponsors.ContainsKey(e.UserId), "Cached data was found on client connect");

        _cachedSponsors[e.UserId] = info.Value;
    }
    
    private void OnDisconnect(object? sender, NetDisconnectedArgs e)
    {
        _cachedSponsors.Remove(e.Channel.UserId);
    }

    private async Task<SponsorInfo?> LoadSponsorInfo(NetUserId userId)
    {
        if (string.IsNullOrEmpty(_apiUrl))
            return null;

        var url = $"{_apiUrl}/sponsors/{userId.ToString()}";
        var response = await _httpClient.GetAsync(url);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        if (response.StatusCode != HttpStatusCode.OK)
        {
            var errorText = await response.Content.ReadAsStringAsync();
            _sawmill.Error(
                "Failed to get player sponsor OOC color from API: [{StatusCode}] {Response}",
                response.StatusCode,
                errorText);
            return null;
        }

        return await response.Content.ReadFromJsonAsync<SponsorInfo>();
    }
    

    public struct SponsorInfo
    {
        [JsonPropertyName("tier")]
        public int? Tier { get; set; }

        [JsonPropertyName("oocColor")]
        public string? OOCColor { get; set; }

        [JsonPropertyName("priorityJoin")]
        public bool HavePriorityJoin { get; set; }

        [JsonPropertyName("nekoCharName")]
        public string? NekoCharName { get; set; }
    }
}