﻿#region Header
//   Vorspire    _,-'/-'/  WebStats.cs
//   .      __,-; ,'( '/
//    \.    `-.__`-._`:_,-._       _ , . ``
//     `:-._,------' ` _,`--` -: `_ , ` ,' :
//        `---..__,,--'  (C) 2016  ` -'. -'
//        #  Vita-Nex [http://core.vita-nex.com]  #
//  {o)xxx|===============-   #   -===============|xxx(o}
//        #        The MIT License (MIT)          #
#endregion

#region References
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;

using Server;
using Server.Guilds;
using Server.Items;
using Server.Misc;
using Server.Network;

using VitaNex.IO;
using VitaNex.Text;
using VitaNex.Web;
#endregion

namespace VitaNex.Modules.WebStats
{
	public static partial class WebStats
	{
		public const AccessLevel Access = AccessLevel.Administrator;

		public static WebStatsOptions CMOptions { get; private set; }

		private static DateTime _LastUpdate = DateTime.MinValue;
		private static WebStatsRequestFlags _LastFlags = WebStatsRequestFlags.None;

		public static BinaryDataStore<string, WebStatsEntry> Stats { get; private set; }

		public static Dictionary<IPAddress, List<Mobile>> Snapshot { get; private set; }

		private static readonly Dictionary<string, object> _Json;
		private static readonly Bitmap _Banner;

		public static Action<Bitmap, Graphics> BannerHandler { get; set; }

		public static string JsonResponse { get; private set; }
		public static byte[] BannerResponse { get; private set; }

		private static bool _UpdatingBanner;
		private static bool _UpdatingJson;
		private static bool _Updating;

		private static void HandleWebRequest(WebAPIContext context)
		{
			if (context.Request.Queries["banner"] != null)
			{
				context.Response.Data = GetBanner(false);
				context.Response.ContentType = "png";
			}
			else
			{
				WebStatsRequestFlags flags;

				if (context.Request.Queries.Count > 0)
				{
					if (context.Request.Queries["flags"] != null)
					{
						var f = context.Request.Queries["flags"];
						int v;

						if (f.StartsWith("0x"))
						{
							if (!Int32.TryParse(f.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out v) || v < 0)
							{
								v = 0;
							}
						}
						else if (!Int32.TryParse(f, out v) || v < 0)
						{
							v = 0;
						}

						flags = (WebStatsRequestFlags)v;
					}
					else
					{
						flags = WebStatsRequestFlags.Server | WebStatsRequestFlags.Stats | WebStatsRequestFlags.Players;

						bool? server = null, stats = null, players = null;

						foreach (var q in context.Request.Queries)
						{
							var value = !q.Value.EqualsAny(true, "false", "no", "off", "disabled", "0", String.Empty);

							if (Insensitive.Equals(q.Key, "server"))
							{
								server = value;
							}
							else if (Insensitive.Equals(q.Key, "stats"))
							{
								stats = value;
							}
							else if (Insensitive.Equals(q.Key, "players"))
							{
								players = value;
							}
						}

						if (server != null && !server.Value)
						{
							flags &= ~WebStatsRequestFlags.Server;
						}

						if (stats != null && !stats.Value)
						{
							flags &= ~WebStatsRequestFlags.Stats;
						}

						if (players != null && !players.Value)
						{
							flags &= ~WebStatsRequestFlags.Players;
						}
					}
				}
				else
				{
					flags = WebStatsRequestFlags.Server | WebStatsRequestFlags.Stats | WebStatsRequestFlags.Players;
				}

				context.Response.Data = GetJson(flags, false);
				context.Response.ContentType = "json";
			}
		}

		public static bool UpdateStats(bool forceUpdate)
		{
			if (_Updating)
			{
				return false;
			}

			if (!forceUpdate && _LastFlags == CMOptions.RequestFlags && DateTime.UtcNow - _LastUpdate < CMOptions.UpdateInterval)
			{
				return false;
			}

			_Updating = true;

			_LastUpdate = DateTime.UtcNow;
			_LastFlags = CMOptions.RequestFlags;

			var states = NetState.Instances.Where(ns => ns != null && ns.Socket != null && ns.Mobile != null).ToArray();

			Snapshot.Clear();

			foreach (var ns in states)
			{
				var ep = (IPEndPoint)ns.Socket.RemoteEndPoint;

				List<Mobile> ch;

				if (!Snapshot.TryGetValue(ep.Address, out ch) || ch == null)
				{
					Snapshot[ep.Address] = ch = new List<Mobile>();
				}

				ch.Add(ns.Mobile);
			}

			TimeSpan time;
			long lnum;
			int num;

			#region Uptime
			var uptime = DateTime.UtcNow - Clock.ServerStart;

			Stats["uptime"].Value = uptime;
			Stats["uptime_peak"].Value = Stats["uptime_peak"].TryCast(out time)
				? TimeSpan.FromSeconds(Math.Max(time.TotalSeconds, uptime.TotalSeconds))
				: uptime;
			#endregion

			#region Online
			var connected = states.Length;

			Stats["online"].Value = connected;
			Stats["online_max"].Value = Stats["online_max"].TryCast(out num) ? Math.Max(num, connected) : connected;
			Stats["online_peak"].Value = Stats["online_peak"].TryCast(out num) ? Math.Max(num, connected) : connected;
			#endregion

			#region Unique
			var unique = Snapshot.Count;

			Stats["unique"].Value = unique;
			Stats["unique_max"].Value = Stats["unique_max"].TryCast(out num) ? Math.Max(num, unique) : unique;
			Stats["unique_peak"].Value = Stats["unique_peak"].TryCast(out num) ? Math.Max(num, unique) : unique;
			#endregion

			#region Items
			var items = World.Items.Count;

			Stats["items"].Value = items;
			Stats["items_max"].Value = Stats["items_max"].TryCast(out num) ? Math.Max(num, items) : items;
			Stats["items_peak"].Value = Stats["items_peak"].TryCast(out num) ? Math.Max(num, items) : items;
			#endregion

			#region Mobiles
			var mobiles = World.Mobiles.Count;

			Stats["mobiles"].Value = mobiles;
			Stats["mobiles_max"].Value = Stats["mobiles_max"].TryCast(out num) ? Math.Max(num, mobiles) : mobiles;
			Stats["mobiles_peak"].Value = Stats["mobiles_peak"].TryCast(out num) ? Math.Max(num, mobiles) : mobiles;
			#endregion

			#region Guilds
			var guilds = BaseGuild.List.Count;

			Stats["guilds"].Value = guilds;
			Stats["guilds_max"].Value = Stats["guilds_max"].TryCast(out num) ? Math.Max(num, guilds) : guilds;
			Stats["guilds_peak"].Value = Stats["guilds_peak"].TryCast(out num) ? Math.Max(num, guilds) : guilds;
			#endregion

			#region Misc
			var ram = GC.GetTotalMemory(false);

			Stats["memory"].Value = ram;
			Stats["memory_max"].Value = Stats["memory_max"].TryCast(out lnum) ? Math.Max(lnum, ram) : ram;
			Stats["memory_peak"].Value = Stats["memory_peak"].TryCast(out lnum) ? Math.Max(lnum, ram) : ram;
			#endregion

			_Updating = false;

			return true;
		}

		private static byte[] GetBanner(bool forceUpdate)
		{
			if (_UpdatingBanner || !UpdateStats(forceUpdate))
			{
				return BannerResponse;
			}

			_UpdatingBanner = true;

			VitaNexCore.TryCatch(
				() =>
				{
					using (var g = Graphics.FromImage(_Banner))
					{
						g.Clear(Color.Transparent);

						if (BannerHandler != null)
						{
							BannerHandler(_Banner, g);
						}
						else
						{
							var hf = new Font("Tahoma", 20f);
							var nf = new Font("Tahoma", 10f);

							var hp = new Pen(Brushes.Red);
							var np = new Pen(Brushes.Black);

							g.DrawRectangle(hp, 0f, 0f, _Banner.Width - 1f, _Banner.Height - 1f);

							g.DrawString(ServerList.ServerName, hf, hp.Brush, 5f, 2.5f);

							var ipep = Listener.EndPoints.LastOrDefault();

							if (ipep != null)
							{
								var ipp = IPAddressExtUtility.FindPublic();

								if (ipp != null)
								{
									g.DrawString(String.Format("{0}:{1}", ipp, ipep.Port), nf, np.Brush, 10f, 35f);
								}
								else
								{
									foreach (var ip in ipep.Address.FindInternal())
									{
										g.DrawString(String.Format("{0}:{1}", ip, ipep.Port), nf, np.Brush, 10f, 35f);
										break;
									}
								}
							}
						}

						g.Save();

						using (var ms = new MemoryStream())
						{
							_Banner.Save(ms, ImageFormat.Png);

							BannerResponse = ms.ToArray();
						}

						_Banner.Save(IOUtility.GetSafeFilePath(VitaNexCore.CacheDirectory + "/WebStats.png", true), ImageFormat.Png);
					}
				},
				CMOptions.ToConsole);

			_UpdatingBanner = false;

			return BannerResponse;
		}

		private static string GetJson(WebStatsRequestFlags flags, bool forceUpdate)
		{
			if (UpdateJson(forceUpdate))
			{
				if (flags != WebStatsRequestFlags.None && flags != WebStatsRequestFlags.All)
				{
					var root = new Dictionary<string, object>();

					if (flags.HasFlag(WebStatsRequestFlags.Server))
					{
						root["server"] = _Json.GetValue("server");
					}

					if (flags.HasFlag(WebStatsRequestFlags.Stats))
					{
						root["stats"] = _Json.GetValue("stats");
					}

					if (flags.HasFlag(WebStatsRequestFlags.Players))
					{
						root["players"] = _Json.GetValue("players");
					}

					var response = Json.Encode(root);

					root.Clear();

					return response;
				}
			}

			return flags == WebStatsRequestFlags.None ? String.Empty : JsonResponse;
		}

		private static bool UpdateJson(bool forceUpdate)
		{
			if (_UpdatingJson || !UpdateStats(forceUpdate))
			{
				return false;
			}

			_UpdatingJson = true;

			VitaNexCore.TryCatch(
				() =>
				{
					var root = _Json;

					root["vnc_version"] = VitaNexCore.Version.Value;
					root["mod_version"] = CMOptions.ModuleVersion;

					root["last_update"] = _LastUpdate.ToString(CultureInfo.InvariantCulture);
					root["last_update_stamp"] = _LastUpdate.ToTimeStamp().Stamp;

					if (CMOptions.DisplayServer)
					{
						var server = root.Intern("server", o => o as Dictionary<string, object> ?? new Dictionary<string, object>());

						server["name"] = ServerList.ServerName;

						var ipep = Listener.EndPoints.LastOrDefault();

						if (ipep != null)
						{
							var ipp = IPAddressExtUtility.FindPublic();

							if (ipp != null)
							{
								server["host"] = ipp.ToString();
								server["port"] = ipep.Port;
							}
							else
							{
								foreach (var ip in ipep.Address.FindInternal())
								{
									server["host"] = ip.ToString();
									server["port"] = ipep.Port;
									break;
								}
							}
						}

						server["os"] = Environment.OSVersion.VersionString;
						server["net"] = Environment.Version.ToString();

#if SERVUO
						server["core"] = "ServUO";
#elif JUSTUO
						server["core"] = "JustUO";
#else
						server["core"] = "RunUO";
#endif

						var an = Core.Assembly.GetName();

						server["assembly"] = an.Name;
						server["assembly_version"] = an.Version.ToString();

						root["server"] = server;
					}
					else
					{
						var server = root.Intern("server", o => o as Dictionary<string, object>);

						if (server != null)
						{
							server.Clear();
						}

						root.Remove("server");
					}

					if (CMOptions.DisplayStats)
					{
						var stats = root.Intern("stats", o => o as Dictionary<string, object> ?? new Dictionary<string, object>());

						foreach (var kv in Stats)
						{
							var o = kv.Value.Value;

							if (o is DateTime)
							{
								var dt = (DateTime)o;

								stats[kv.Key] = dt.ToString(CultureInfo.InvariantCulture);
								stats[kv.Key + "_stamp"] = Math.Floor(dt.ToTimeStamp().Stamp);
							}
							else if (o is TimeSpan)
							{
								var ts = (TimeSpan)o;

								stats[kv.Key] = ts.ToString();
								stats[kv.Key + "_stamp"] = ts.TotalSeconds;
							}
							else
							{
								stats[kv.Key] = o;
							}
						}

						root["stats"] = stats;
					}
					else
					{
						var stats = root.Intern("stats", o => o as Dictionary<string, object>);

						if (stats != null)
						{
							stats.Clear();
						}

						root.Remove("stats");
					}

					if (CMOptions.DisplayPlayers)
					{
						var players = root.Intern("players", o => o as List<object> ?? new List<object>());

						var playerIndex = 0;

						foreach (var p in Snapshot.SelectMany(s => s.Value))
						{
							var player = players.Intern(
								playerIndex,
								o => o as Dictionary<string, object> ?? new Dictionary<string, object>());

							player["id"] = p.Serial.Value;
							player["name"] = p.RawName ?? String.Empty;
							player["title"] = p.Title ?? String.Empty;
							player["profile"] = p.Profile ?? String.Empty;

							player["fame"] = p.Fame;
							player["karma"] = p.Karma;
							player["kills"] = p.Kills;

							if (CMOptions.DisplayPlayerGuilds && p.Guild != null)
							{
								var guild = player.Intern("guild", o => o as Dictionary<string, object> ?? new Dictionary<string, object>());

								guild["id"] = p.Guild.Id;
								guild["name"] = p.Guild.Name ?? String.Empty;
								guild["abbr"] = p.Guild.Abbreviation ?? String.Empty;

								player["guild"] = guild;
							}
							else
							{
								var guild = player.Intern("guild", o => o as Dictionary<string, object>);

								if (guild != null)
								{
									guild.Clear();
								}

								player.Remove("guild");
							}

							if (CMOptions.DisplayPlayerStats)
							{
								var stats = player.Intern("stats", o => o as Dictionary<string, object> ?? new Dictionary<string, object>());

								stats["cap"] = p.StatCap;
								stats["total"] = p.RawStatTotal;

								stats["str"] = p.Str;
								stats["str_raw"] = p.RawStr;

								stats["dex"] = p.Dex;
								stats["dex_raw"] = p.RawDex;

								stats["int"] = p.Int;
								stats["int_raw"] = p.RawInt;

								stats["hits"] = p.Hits;
								stats["hits_max"] = p.HitsMax;

								stats["stam"] = p.Stam;
								stats["stam_max"] = p.StamMax;

								stats["mana"] = p.Mana;
								stats["mana_max"] = p.ManaMax;

								player["stats"] = stats;
							}
							else
							{
								var stats = player.Intern("stats", o => o as Dictionary<string, object>);

								if (stats != null)
								{
									stats.Clear();
								}

								player.Remove("stats");
							}

							if (CMOptions.DisplayPlayerSkills)
							{
								var skills = player.Intern("skills", o => o as List<object> ?? new List<object>());

								var skillIndex = 0;

								foreach (var s in SkillInfo.Table)
								{
									var skill = skills.Intern(skillIndex, o => o as Dictionary<string, object> ?? new Dictionary<string, object>());

									skill["id"] = s.SkillID;
									skill["name"] = s.Name;
									skill["base"] = p.Skills[s.SkillID].Base;
									skill["value"] = p.Skills[s.SkillID].Value;
									skill["cap"] = p.Skills[s.SkillID].Cap;

									skills.AddOrReplace(skill);

									++skillIndex;
								}

								skills.TrimEndTo(skillIndex);

								player["skills"] = skills;
							}
							else
							{
								var skills = player.Intern("skills", o => o as List<object>);

								if (skills != null)
								{
									skills.Free(true);
								}

								player.Remove("skills");
							}

							if (CMOptions.DisplayPlayerEquip)
							{
								var equip = player.Intern("equip", o => o as List<object> ?? new List<object>());

								var equipIndex = 0;

								foreach (var i in p.Items)
								{
									var item = equip.Intern(equipIndex, o => o as Dictionary<string, object> ?? new Dictionary<string, object>());

									item["id"] = i.Serial.Value;
									item["type"] = i.GetType().Name;
									item["layer"] = (int)i.Layer;
									item["art"] = i.ItemID;
									item["hue"] = i.Hue;
									item["name"] = i.ResolveName();

									equip.AddOrReplace(item);

									++equipIndex;
								}

								equip.TrimEndTo(equipIndex);

								player["equip"] = equip;
							}
							else
							{
								var equip = player.Intern("equip", o => o as List<object>);

								if (equip != null)
								{
									equip.Free(true);
								}

								player.Remove("equip");
							}

							players.AddOrReplace(player);

							++playerIndex;
						}

						players.TrimEndTo(playerIndex);

						root["players"] = players;
					}
					else
					{
						var players = root.Intern("players", o => o as List<object>);

						if (players != null)
						{
							players.Free(true);
						}

						root.Remove("players");
					}

					JsonResponse = Json.Encode(root);

					File.WriteAllText(IOUtility.GetSafeFilePath(VitaNexCore.CacheDirectory + "/WebStats.json", true), JsonResponse);
				},
				CMOptions.ToConsole);

			_UpdatingJson = false;

			return true;
		}
	}
}