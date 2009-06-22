using System;
using System.Collections.Generic;
using MonoTorrent.Client;
using MonoTorrent.ClientService.Configuration;
using Newtonsoft.Json;

namespace MonoTorrent.ClientService
{
	/// <summary>
	/// Adapter between WebUI and MonoTorrent settings. 
	/// </summary>
    internal class SettingsAdapter
	{
        #region Configuration section references
        private WebUISection webui;
        private MonoTorrentClientSection torrent;
        #endregion

        /// <summary>
        /// Handlers for each setting.
        /// </summary>
        Dictionary<string, Setting> handlers;

        /// <summary>
        /// Creates the setting adapter for the whole system.
        /// </summary>
		public SettingsAdapter(
            ConfiguredServiceBase<WebUISection> webuiService,
            ConfiguredServiceBase<MonoTorrentClientSection> monoTorrentService)
		{
            this.handlers = new Dictionary<string, Setting>();

            this.webui = webuiService.Config;
            this.torrent = monoTorrentService.Config;

            webuiService.ConfigurationLoaded += 
                (sender, e) => { this.webui = ((ConfiguredServiceBase<WebUISection>)sender).Config; };

            monoTorrentService.ConfigurationLoaded +=
                (sender, e) => { this.torrent = ((ConfiguredServiceBase<MonoTorrentClientSection>)sender).Config; };
            
            InitializeSettingMappings();
		}

        /// <summary>
        /// Mapping logic for all setting is defined here.
        /// </summary>
        private void InitializeSettingMappings()
        {
            /* Intended usage:
             *                      // WebUI name  // type of value
             *      RegisterSetting("max_dl_rate", WebSettingType.Integer,
             *          ()    => torrent.MaxDownloadRate,                   // getter function
             *          value => torrent.MaxDownloadRate = int.Parse(value) // setter function
             *          );
            */

            RegisterSetting("torrents_start_stopped", WebSettingType.Boolean,
                ()    => false,
                value => bool.Parse(value)
                );

            RegisterSetting("confirm_when_deleting", WebSettingType.Boolean,
                ()    => true,
                value => bool.Parse(value)
                );

            RegisterSetting("install_revision", WebSettingType.Integer,
                ()    => webui.BuildNumber /*15658*/,
                value => webui.BuildNumber = int.Parse(value)
                );

            RegisterSetting("confirm_exit", WebSettingType.Boolean,
                ()    => null /*true*/,
                value => bool.Parse(value)
                );

            RegisterSetting("close_to_tray", WebSettingType.Boolean,
                ()    => null /*true*/,
                value => bool.Parse(value)
                );

            RegisterSetting("minimize_to_tray", WebSettingType.Boolean,
                ()    => null /*true*/,
                value => bool.Parse(value)
                );

            RegisterSetting("tray_activate", WebSettingType.Boolean,
                ()    => true,
                value => bool.Parse(value)
                );

            RegisterSetting("tray.show", WebSettingType.Boolean,
                ()    => true,
                value => bool.Parse(value)
                );

            RegisterSetting("tray.single_click", WebSettingType.Boolean,
                ()    => false,
                value => bool.Parse(value)
                );

            RegisterSetting("activate_on_file", WebSettingType.Boolean,
                ()    => true,
                value => bool.Parse(value)
                );

            RegisterSetting("confirm_remove_tracker", WebSettingType.Boolean,
                ()    => true,
                value => bool.Parse(value)
                );

            RegisterSetting("check_assoc_on_start", WebSettingType.Boolean,
                ()    => true,
                value => bool.Parse(value)
                );

            RegisterSetting("reload_freq", WebSettingType.Integer,
                ()    => 0,
                value => int.Parse(value)
                );

            RegisterSetting("bind_port", WebSettingType.Integer,
                ()    => 15052,
                value => int.Parse(value)
                );

            RegisterSetting("tracker_ip", WebSettingType.String,
                ()    => "",
                value => String.IsNullOrEmpty(value)
                );

            RegisterSetting("dir_active_download_flag", WebSettingType.Boolean,
                ()    => false,
                value => bool.Parse(value)
                );

            RegisterSetting("dir_torrent_files_flag", WebSettingType.Boolean,
                ()    => false,
                value => bool.Parse(value)
                );

            RegisterSetting("dir_completed_download_flag", WebSettingType.Boolean,
                ()    => false,
                value => bool.Parse(value)
                );

            RegisterSetting("dir_completed_torrents_flag", WebSettingType.Boolean,
                ()    => false,
                value => bool.Parse(value)
                );

            RegisterSetting("dir_active_download", WebSettingType.String,
                ()    => "",
                value => String.IsNullOrEmpty(value)
                );

            RegisterSetting("dir_torrent_files", WebSettingType.String,
                ()    => "",
                value => String.IsNullOrEmpty(value)
                );

            RegisterSetting("dir_completed_download", WebSettingType.String,
                ()    => "",
                value => String.IsNullOrEmpty(value)
                );

            RegisterSetting("dir_completed_torrents", WebSettingType.String,
                ()    => "",
                value => String.IsNullOrEmpty(value)
                );

            RegisterSetting("dir_add_label", WebSettingType.Boolean,
                ()    => false,
                value => bool.Parse(value)
                );

            RegisterSetting("max_dl_rate", WebSettingType.Integer,
                ()    => torrent.MaxDownloadRate,
                value => torrent.MaxDownloadRate = int.Parse(value)
                );
            
            RegisterSetting("max_ul_rate", WebSettingType.Integer,
                ()    => 0,
                value => int.Parse(value)
                );

            RegisterSetting("max_ul_rate_seed", WebSettingType.Integer,
                ()    => 0,
                value => int.Parse(value)
                );

            RegisterSetting("max_ul_rate_seed_flag", WebSettingType.Boolean,
                ()    => false,
                value => bool.Parse(value)
                );

            RegisterSetting("ul_auto_throttle", WebSettingType.Boolean,
                ()    => false,
                value => bool.Parse(value)
                );

            RegisterSetting("gui.ulrate_menu", WebSettingType.String,
                ()    => "0,5,10,15,20,30,40,50,100,150,200,300,400,500",
                value => String.IsNullOrEmpty(value)
                );

            RegisterSetting("gui.dlrate_menu", WebSettingType.String,
                ()    => "0,5,10,15,20,30,40,50,100,150,200,300,400,500",
                value => String.IsNullOrEmpty(value)
                );

            RegisterSetting("gui.manual_ratemenu", WebSettingType.Boolean,
                ()    => false,
                value => bool.Parse(value)
                );

            RegisterSetting("gui.persistent_labels", WebSettingType.String,
                ()    => "",
                value => String.IsNullOrEmpty(value)
                );

            RegisterSetting("gui.compat_diropen", WebSettingType.Boolean,
                ()    => false,
                value => bool.Parse(value)
                );

            RegisterSetting("gui.alternate_color", WebSettingType.Boolean,
                ()    => false,
                value => bool.Parse(value)
                );

            RegisterSetting("sys.prevent_standby", WebSettingType.Boolean,
                ()    => true,
                value => bool.Parse(value)
                );

            RegisterSetting("sys.enable_wine_hacks", WebSettingType.Boolean,
                ()    => true,
                value => bool.Parse(value)
                );

            RegisterSetting("ul_slots_per_torrent", WebSettingType.Integer,
                ()    => 4,
                value => int.Parse(value)
                );

            RegisterSetting("conns_per_torrent", WebSettingType.Integer,
                ()    => 50,
                value => int.Parse(value)
                );

            RegisterSetting("conns_globally", WebSettingType.Integer,
                ()    => 200,
                value => int.Parse(value)
                );

            RegisterSetting("max_active_torrent", WebSettingType.Integer,
                ()    => 8,
                value => int.Parse(value)
                );

            RegisterSetting("max_active_downloads", WebSettingType.Integer,
                ()    => 5,
                value => int.Parse(value)
                );

            RegisterSetting("seed_prio_limitul", WebSettingType.Integer,
                ()    => 4,
                value => int.Parse(value)
                );

            RegisterSetting("seed_prio_limitul_flag", WebSettingType.Boolean,
                ()    => false,
                value => bool.Parse(value)
                );

            RegisterSetting("seeds_prioritized", WebSettingType.Boolean,
                ()    => false,
                value => bool.Parse(value)
                );

            RegisterSetting("seed_ratio", WebSettingType.Integer,
                ()    => 1500,
                value => int.Parse(value)
                );

            RegisterSetting("seed_time", WebSettingType.Integer,
                ()    => 0,
                value => int.Parse(value)
                );

            RegisterSetting("move_if_defdir", WebSettingType.Boolean,
                ()    => true,
                value => bool.Parse(value)
                );

            RegisterSetting("mainwnd_split", WebSettingType.Integer,
                ()    => 172,
                value => int.Parse(value)
                );

            RegisterSetting("mainwnd_split_x", WebSettingType.Integer,
                ()    => 110,
                value => int.Parse(value)
                );

            RegisterSetting("resolve_peerips", WebSettingType.Boolean,
                ()    => true,
                value => bool.Parse(value)
                );

            RegisterSetting("check_update", WebSettingType.Boolean,
                ()    => true,
                value => bool.Parse(value)
                );

            RegisterSetting("check_update_beta", WebSettingType.Boolean,
                ()    => false,
                value => bool.Parse(value)
                );

            RegisterSetting("anoninfo", WebSettingType.Boolean,
                ()    => true,
                value => bool.Parse(value)
                );

            RegisterSetting("upnp", WebSettingType.Boolean,
                ()    => true,
                value => bool.Parse(value)
                );

            RegisterSetting("natpmp", WebSettingType.Boolean,
                ()    => true,
                value => bool.Parse(value)
                );

            RegisterSetting("lsd", WebSettingType.Boolean,
                ()    => true,
                value => bool.Parse(value)
                );

            RegisterSetting("disable_fw", WebSettingType.Boolean,
                ()    => true,
                value => bool.Parse(value)
                );

            RegisterSetting("k", WebSettingType.String,
                ()    => "",
                value => String.IsNullOrEmpty(value)
                );

            RegisterSetting("v", WebSettingType.Integer,
                ()    => 0,
                value => int.Parse(value)
                );

            RegisterSetting("pd", WebSettingType.Integer,
                ()    => 0,
                value => int.Parse(value)
                );

            RegisterSetting("pu", WebSettingType.Integer,
                ()    => 0,
                value => int.Parse(value)
                );

            RegisterSetting("asip", WebSettingType.String,
                ()    => "",
                value => String.IsNullOrEmpty(value)
                );

            RegisterSetting("asdns", WebSettingType.Integer,
                ()    => 0,
                value => int.Parse(value)
                );

            RegisterSetting("ascon", WebSettingType.Integer,
                ()    => 0,
                value => int.Parse(value)
                );

            RegisterSetting("asdl", WebSettingType.Integer,
                ()    => 0,
                value => int.Parse(value)
                );

            RegisterSetting("sched_enable", WebSettingType.Boolean,
                ()    => false,
                value => bool.Parse(value)
                );

            RegisterSetting("sched_ul_rate", WebSettingType.Integer,
                ()    => 0,
                value => int.Parse(value)
                );

            RegisterSetting("sched_dl_rate", WebSettingType.Integer,
                ()    => 0,
                value => int.Parse(value)
                );

            RegisterSetting("sched_dis_dht", WebSettingType.Boolean,
                ()    => true,
                value => bool.Parse(value)
                );

            RegisterSetting("webui.remote_enable", WebSettingType.Boolean,
                ()    => false,
                value => bool.Parse(value)
                );

            RegisterSetting("enable_scrape", WebSettingType.Boolean,
                ()    => true,
                value => bool.Parse(value)
                );

            RegisterSetting("show_toolbar", WebSettingType.Boolean,
                ()    => true,
                value => bool.Parse(value)
                );

            RegisterSetting("show_details", WebSettingType.Boolean,
                ()    => true,
                value => bool.Parse(value)
                );

            RegisterSetting("show_status", WebSettingType.Boolean,
                ()    => true,
                value => bool.Parse(value)
                );

            RegisterSetting("show_category", WebSettingType.Boolean,
                ()    => true,
                value => bool.Parse(value)
                );

            RegisterSetting("show_tabicons", WebSettingType.Boolean,
                ()    => true,
                value => bool.Parse(value)
                );

            RegisterSetting("rand_port_on_start", WebSettingType.Boolean,
                ()    => false,
                value => bool.Parse(value)
                );

            RegisterSetting("prealloc_space", WebSettingType.Boolean,
                ()    => false,
                value => bool.Parse(value)
                );

            RegisterSetting("language", WebSettingType.Integer,
                ()    => -1,
                value => int.Parse(value)
                );

            RegisterSetting("logger_mask", WebSettingType.Integer,
                ()    => 0,
                value => int.Parse(value)
                );

            RegisterSetting("autostart", WebSettingType.Boolean,
                ()    => false,
                value => bool.Parse(value)
                );

            RegisterSetting("dht", WebSettingType.Boolean,
                ()    => true,
                value => bool.Parse(value)
                );

            RegisterSetting("dht_per_torrent", WebSettingType.Boolean,
                ()    => true,
                value => bool.Parse(value)
                );

            RegisterSetting("pex", WebSettingType.Boolean,
                ()    => true,
                value => bool.Parse(value)
                );

            RegisterSetting("rate_limit_local_peers", WebSettingType.Boolean,
                ()    => false,
                value => bool.Parse(value)
                );

            RegisterSetting("net.bind_ip", WebSettingType.String,
                ()    => "",
                value => String.IsNullOrEmpty(value)
                );

            RegisterSetting("net.outgoing_ip", WebSettingType.String,
                ()    => "",
                value => String.IsNullOrEmpty(value)
                );

            RegisterSetting("net.outgoing_port", WebSettingType.Integer,
                ()    => 0,
                value => int.Parse(value)
                );

            RegisterSetting("net.outgoing_max_port", WebSettingType.Integer,
                ()    => 0,
                value => int.Parse(value)
                );

            RegisterSetting("net.low_cpu", WebSettingType.Boolean,
                ()    => false,
                value => bool.Parse(value)
                );

            RegisterSetting("net.calc_overhead", WebSettingType.Boolean,
                ()    => false,
                value => bool.Parse(value)
                );

            RegisterSetting("net.max_halfopen", WebSettingType.Integer,
                ()    => 8,
                value => int.Parse(value)
                );

            RegisterSetting("net.wsaevents", WebSettingType.Integer,
                ()    => 6,
                value => int.Parse(value)
                );

            RegisterSetting("net.upnp_tcp_only", WebSettingType.Boolean,
                ()    => false,
                value => bool.Parse(value)
                );

            RegisterSetting("isp.bep22", WebSettingType.Boolean,
                ()    => false,
                value => bool.Parse(value)
                );

            RegisterSetting("dir_autoload_flag", WebSettingType.Boolean,
                ()    => false,
                value => bool.Parse(value)
                );

            RegisterSetting("dir_autoload_delete", WebSettingType.Boolean,
                ()    => false,
                value => bool.Parse(value)
                );

            RegisterSetting("dir_autoload", WebSettingType.String,
                ()    => "",
                value => String.IsNullOrEmpty(value)
                );

            RegisterSetting("notify_complete", WebSettingType.Boolean,
                ()    => true,
                value => bool.Parse(value)
                );

            RegisterSetting("extra_ulslots", WebSettingType.Boolean,
                ()    => true,
                value => bool.Parse(value)
                );

            RegisterSetting("ipfilter.enable", WebSettingType.Boolean,
                ()    => true,
                value => bool.Parse(value)
                );

            RegisterSetting("dht.rate", WebSettingType.Integer,
                ()    => -1,
                value => int.Parse(value)
                );

            RegisterSetting("extras", WebSettingType.Integer,
                ()    => 2,
                value => int.Parse(value)
                );

            RegisterSetting("score", WebSettingType.Integer,
                ()    => 0,
                value => int.Parse(value)
                );

            RegisterSetting("append_incomplete", WebSettingType.Boolean,
                ()    => false,
                value => bool.Parse(value)
                );

            RegisterSetting("show_add_dialog", WebSettingType.Boolean,
                ()    => true,
                value => bool.Parse(value)
                );

            RegisterSetting("always_show_add_dialog", WebSettingType.Boolean,
                ()    => true,
                value => bool.Parse(value)
                );

            RegisterSetting("gui.log_date", WebSettingType.Boolean,
                ()    => true,
                value => bool.Parse(value)
                );

            RegisterSetting("ct_hist_comm", WebSettingType.String,
                ()    => "",
                value => String.IsNullOrEmpty(value)
                );

            RegisterSetting("ct_hist_flags", WebSettingType.Integer,
                ()    => 0,
                value => int.Parse(value)
                );

            RegisterSetting("ct_hist_skip", WebSettingType.String,
                ()    => "",
                value => String.IsNullOrEmpty(value)
                );

            RegisterSetting("boss_key", WebSettingType.Integer,
                ()    => 0,
                value => int.Parse(value)
                );

            RegisterSetting("encryption_mode", WebSettingType.Integer,
                ()    => 0,
                value => int.Parse(value)
                );

            RegisterSetting("encryption_allow_legacy", WebSettingType.Boolean,
                ()    => true,
                value => bool.Parse(value)
                );

            RegisterSetting("rss.update_interval", WebSettingType.Integer,
                ()    => 15,
                value => int.Parse(value)
                );

            RegisterSetting("rss.smart_repack_filter", WebSettingType.Boolean,
                ()    => true,
                value => bool.Parse(value)
                );

            RegisterSetting("rss.feed_as_default_label", WebSettingType.Boolean,
                ()    => true,
                value => bool.Parse(value)
                );

            RegisterSetting("gui.dblclick_seed", WebSettingType.Integer,
                ()    => 0,
                value => int.Parse(value)
                );

            RegisterSetting("gui.dblclick_dl", WebSettingType.Integer,
                ()    => 0,
                value => int.Parse(value)
                );

            RegisterSetting("gui.update_rate", WebSettingType.Integer,
                ()    => 1000,
                value => int.Parse(value)
                );

            RegisterSetting("gui.sg_mode", WebSettingType.Integer,
                ()    => 1,
                value => int.Parse(value)
                );

            RegisterSetting("gui.delete_to_trash", WebSettingType.Boolean,
                ()    => true,
                value => bool.Parse(value)
                );

            RegisterSetting("gui.default_del_action", WebSettingType.Integer,
                ()    => 0,
                value => int.Parse(value)
                );

            RegisterSetting("gui.speed_in_title", WebSettingType.Boolean,
                ()    => false,
                value => bool.Parse(value)
                );

            RegisterSetting("gui.limits_in_statusbar", WebSettingType.Boolean,
                ()    => false,
                value => bool.Parse(value)
                );

            RegisterSetting("gui.graphic_progress", WebSettingType.Boolean,
                ()    => true,
                value => bool.Parse(value)
                );

            RegisterSetting("gui.piecebar_progress", WebSettingType.Boolean,
                ()    => false,
                value => bool.Parse(value)
                );

            RegisterSetting("gui.tall_category_list", WebSettingType.Boolean,
                ()    => true,
                value => bool.Parse(value)
                );

            RegisterSetting("gui.bypass_search_redirect", WebSettingType.Boolean,
                ()    => false,
                value => bool.Parse(value)
                );

            RegisterSetting("gui.last_preference_tab-1.8", WebSettingType.Integer,
                ()    => 8,
                value => int.Parse(value)
                );

            RegisterSetting("gui.last_overview_tab-1.8", WebSettingType.Integer,
                ()    => 0,
                value => int.Parse(value)
                );

            RegisterSetting("queue.dont_count_slow_dl", WebSettingType.Boolean,
                ()    => true,
                value => bool.Parse(value)
                );

            RegisterSetting("queue.dont_count_slow_ul", WebSettingType.Boolean,
                ()    => true,
                value => bool.Parse(value)
                );

            RegisterSetting("queue.slow_dl_threshold", WebSettingType.Integer,
                ()    => 1000,
                value => int.Parse(value)
                );

            RegisterSetting("queue.slow_ul_threshold", WebSettingType.Integer,
                ()    => 1000,
                value => int.Parse(value)
                );

            RegisterSetting("queue.use_seed_peer_ratio", WebSettingType.Boolean,
                ()    => true,
                value => bool.Parse(value)
                );

            RegisterSetting("queue.prio_no_seeds", WebSettingType.Boolean,
                ()    => true,
                value => bool.Parse(value)
                );

            RegisterSetting("bt.auto_ul_interval", WebSettingType.Integer,
                ()    => 600,
                value => int.Parse(value)
                );

            RegisterSetting("bt.auto_ul_sample_window", WebSettingType.Integer,
                ()    => 30,
                value => int.Parse(value)
                );

            RegisterSetting("bt.auto_ul_sample_average", WebSettingType.Integer,
                ()    => 10,
                value => int.Parse(value)
                );

            RegisterSetting("bt.auto_ul_min", WebSettingType.Integer,
                ()    => 8500,
                value => int.Parse(value)
                );

            RegisterSetting("bt.auto_ul_factor", WebSettingType.Integer,
                ()    => 80,
                value => int.Parse(value)
                );

            RegisterSetting("bt.transp_disposition", WebSettingType.Integer,
                ()    => 13,
                value => int.Parse(value)
                );

            RegisterSetting("bt.scrape_stopped", WebSettingType.Boolean,
                ()    => false,
                value => bool.Parse(value)
                );

            RegisterSetting("bt.compact_allocation", WebSettingType.Boolean,
                ()    => false,
                value => bool.Parse(value)
                );

            RegisterSetting("bt.enable_tracker", WebSettingType.Boolean,
                ()    => false,
                value => bool.Parse(value)
                );

            RegisterSetting("bt.multiscrape", WebSettingType.Boolean,
                ()    => true,
                value => bool.Parse(value)
                );

            RegisterSetting("bt.send_have_to_seed", WebSettingType.Boolean,
                ()    => true,
                value => bool.Parse(value)
                );

            RegisterSetting("bt.set_sockbuf", WebSettingType.Boolean,
                ()    => false,
                value => bool.Parse(value)
                );

            RegisterSetting("bt.connect_speed", WebSettingType.Integer,
                ()    => 20,
                value => int.Parse(value)
                );

            RegisterSetting("bt.prio_first_last_piece", WebSettingType.Boolean,
                ()    => false,
                value => bool.Parse(value)
                );

            RegisterSetting("bt.allow_same_ip", WebSettingType.Boolean,
                ()    => false,
                value => bool.Parse(value)
                );

            RegisterSetting("bt.no_connect_to_services", WebSettingType.Boolean,
                ()    => true,
                value => bool.Parse(value)
                );

            RegisterSetting("bt.no_connect_to_services_list", WebSettingType.String,
                ()    => "25,110,6666,6667",
                value => String.IsNullOrEmpty(value)
                );

            RegisterSetting("bt.ban_threshold", WebSettingType.Integer,
                ()    => 3,
                value => int.Parse(value)
                );

            RegisterSetting("bt.use_ban_ratio", WebSettingType.Boolean,
                ()    => true,
                value => bool.Parse(value)
                );

            RegisterSetting("bt.ban_ratio", WebSettingType.Integer,
                ()    => 128,
                value => int.Parse(value)
                );

            RegisterSetting("bt.use_rangeblock", WebSettingType.Boolean,
                ()    => true,
                value => bool.Parse(value)
                );

            RegisterSetting("bt.graceful_shutdown", WebSettingType.Boolean,
                ()    => true,
                value => bool.Parse(value)
                );

            RegisterSetting("bt.shutdown_tracker_timeout", WebSettingType.Integer,
                ()    => 15,
                value => int.Parse(value)
                );

            RegisterSetting("bt.shutdown_upnp_timeout", WebSettingType.Integer,
                ()    => 5,
                value => int.Parse(value)
                );

            RegisterSetting("peer.lazy_bitfield", WebSettingType.Boolean,
                ()    => true,
                value => bool.Parse(value)
                );

            RegisterSetting("peer.resolve_country", WebSettingType.Boolean,
                ()    => false,
                value => bool.Parse(value)
                );

            RegisterSetting("peer.disconnect_inactive", WebSettingType.Boolean,
                ()    => true,
                value => bool.Parse(value)
                );

            RegisterSetting("peer.disconnect_inactive_interval", WebSettingType.Integer,
                ()    => 300,
                value => int.Parse(value)
                );

            RegisterSetting("diskio.flush_files", WebSettingType.Boolean,
                ()    => true,
                value => bool.Parse(value)
                );

            RegisterSetting("diskio.sparse_files", WebSettingType.Boolean,
                ()    => false,
                value => bool.Parse(value)
                );

            RegisterSetting("diskio.no_zero", WebSettingType.Boolean,
                ()    => true,
                value => bool.Parse(value)
                );

            RegisterSetting("diskio.use_partfile", WebSettingType.Boolean,
                ()    => true,
                value => bool.Parse(value)
                );

            RegisterSetting("diskio.smart_hash", WebSettingType.Boolean,
                ()    => true,
                value => bool.Parse(value)
                );

            RegisterSetting("diskio.smart_sparse_hash", WebSettingType.Boolean,
                ()    => true,
                value => bool.Parse(value)
                );

            RegisterSetting("diskio.coalesce_writes", WebSettingType.Boolean,
                ()    => true,
                value => bool.Parse(value)
                );

            RegisterSetting("diskio.coalesce_write_size", WebSettingType.Integer,
                ()    => 2097152,
                value => int.Parse(value)
                );

            RegisterSetting("cache.override", WebSettingType.Boolean,
                ()    => false,
                value => bool.Parse(value)
                );

            RegisterSetting("cache.override_size", WebSettingType.Integer,
                ()    => 32,
                value => int.Parse(value)
                );

            RegisterSetting("cache.reduce", WebSettingType.Boolean,
                ()    => true,
                value => bool.Parse(value)
                );

            RegisterSetting("cache.write", WebSettingType.Boolean,
                ()    => true,
                value => bool.Parse(value)
                );

            RegisterSetting("cache.writeout", WebSettingType.Boolean,
                ()    => true,
                value => bool.Parse(value)
                );

            RegisterSetting("cache.writeimm", WebSettingType.Boolean,
                ()    => true,
                value => bool.Parse(value)
                );

            RegisterSetting("cache.read", WebSettingType.Boolean,
                ()    => true,
                value => bool.Parse(value)
                );

            RegisterSetting("cache.read_turnoff", WebSettingType.Boolean,
                ()    => true,
                value => bool.Parse(value)
                );

            RegisterSetting("cache.read_prune", WebSettingType.Boolean,
                ()    => true,
                value => bool.Parse(value)
                );

            RegisterSetting("cache.read_thrash", WebSettingType.Boolean,
                ()    => false,
                value => bool.Parse(value)
                );

            RegisterSetting("cache.disable_win_read", WebSettingType.Boolean,
                ()    => false,
                value => bool.Parse(value)
                );

            RegisterSetting("cache.disable_win_write", WebSettingType.Boolean,
                ()    => true,
                value => bool.Parse(value)
                );

            RegisterSetting("webui.enable", WebSettingType.Integer,
                ()    => 1,
                value => int.Parse(value)
                );

            RegisterSetting("webui.enable_guest", WebSettingType.Integer,
                ()    => 0,
                value => int.Parse(value)
                );

            RegisterSetting("webui.enable_listen", WebSettingType.Integer,
                ()    => 1,
                value => int.Parse(value)
                );

            RegisterSetting("webui.token_auth", WebSettingType.Boolean,
                ()    => false,
                value => bool.Parse(value)
                );

            RegisterSetting("webui.username", WebSettingType.String,
                ()    => "admin",
                value => String.IsNullOrEmpty(value)
                );

            RegisterSetting("webui.password", WebSettingType.String,
                ()    => "******",
                value => String.IsNullOrEmpty(value)
                );

            RegisterSetting("webui.guest", WebSettingType.String,
                ()    => "guest",
                value => String.IsNullOrEmpty(value)
                );

            RegisterSetting("webui.restrict", WebSettingType.String,
                ()    => "",
                value => String.IsNullOrEmpty(value)
                );

            RegisterSetting("webui.port", WebSettingType.Integer,
                ()    => 8080,
                value => int.Parse(value)
                );

            RegisterSetting("webui.cookie", WebSettingType.String,
                ()    => "{}",
                value => String.IsNullOrEmpty(value)
                );

            RegisterSetting("proxy.proxy", WebSettingType.String,
                ()    => "",
                value => String.IsNullOrEmpty(value)
                );

            RegisterSetting("proxy.type", WebSettingType.Integer,
                ()    => 0,
                value => int.Parse(value)
                );

            RegisterSetting("proxy.port", WebSettingType.Integer,
                ()    => 8080,
                value => int.Parse(value)
                );

            RegisterSetting("proxy.auth", WebSettingType.Boolean,
                ()    => false,
                value => bool.Parse(value)
                );

            RegisterSetting("proxy.p2p", WebSettingType.Boolean,
                ()    => false,
                value => bool.Parse(value)
                );

            RegisterSetting("proxy.resolve", WebSettingType.Boolean,
                ()    => false,
                value => bool.Parse(value)
                );

            RegisterSetting("proxy.username", WebSettingType.String,
                ()    => "",
                value => String.IsNullOrEmpty(value)
                );

            RegisterSetting("proxy.password", WebSettingType.String,
                ()    => "",
                value => String.IsNullOrEmpty(value)
                );
        }

        /// <summary>
        /// Helper method to tidy up syntax in InitializeSettingMappings().
        /// </summary>
        private void RegisterSetting(string name, WebSettingType type, SettingReader get, SettingHandler set)
        {
            handlers.Add(name, new Setting(name, type, get, set));
        }

        /// <summary>
        /// Gets or sets the setting corresponding to <paramref name="settingName"/>
        /// </summary>
        public string this[string settingName]
        {
            get 
            {
                Setting setting;
                if(handlers.TryGetValue(settingName, out setting))
                    return setting.Get().ToString();
                else
                    return null;
            }

            set 
            {
                Setting setting;

                if (handlers.TryGetValue(settingName, out setting))
                    setting.Set(value);
            }
        }

        #region Setting structure
        /// <summary>
        /// Sets the value of the setting.
        /// </summary>
        /// <param name="name">Name of the setting.</param>
        /// <param name="value">Value to set it to.</param>
        /// <returns>True if the value was set sucessfully, false otherwise.</returns>
        private delegate void SettingHandler(string value);

        /// <summary>
        /// Retrieves the value of the setting.
        /// </summary>
        /// <param name="name">Name of the setting.</param>
        /// <returns>Current value of the setting.</returns>
        private delegate object SettingReader();

        /// <summary>
        /// Structure to hold information about a setting.
        /// </summary>
        private class Setting
        {
            private Setting() { }

            public Setting(string name, WebSettingType type, SettingReader get, SettingHandler set)
            {
                this.Name = name;
                this.Type = type;
                this.Get = get;
                this.Set = set;
            }

            public readonly string Name;
            public readonly WebSettingType Type;
            public readonly SettingReader Get;
            public readonly SettingHandler Set;
        }

        /// <summary>
        /// WebUI setting type representation.
        /// </summary>
        enum WebSettingType : int
        {
            Integer = 0,
            Boolean = 1,
            String = 2
        }
        #endregion

        /// <summary>
        /// Prints each registered setting to the <paramref name="writer" />.
        /// Format is consistent with WebUI: ["name",type,"value"]
        /// </summary>
        public void PrintSettings(JsonWriter writer)
        {
            foreach (Setting setting in handlers.Values)
            {
                object value = setting.Get();

                if (value != null)
                {
                    writer.WriteStartArray();

                    writer.WriteValue(setting.Name);
                    writer.WriteValue((int)setting.Type);

                    // .NET Boolean strings representations are "True" and "False"
                    // but JS needs lower case "true" and "false".

                    if(setting.Type == WebSettingType.Boolean)
                        writer.WriteValue(value.ToString().ToLowerInvariant());
                    else
                        writer.WriteValue(value.ToString());

                    writer.WriteEndArray();
                }
            }
        }
    }
}
