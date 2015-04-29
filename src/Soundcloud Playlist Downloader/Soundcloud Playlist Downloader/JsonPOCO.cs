﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Soundcloud_Playlist_Downloader.JsonPoco
{


    public class PlaylistRoot
    {
        public PlaylistItem[] PlaylistItems { get; set; }
    }

    public class PlaylistItem
    {
        public int duration { get; set; }
        public object release_day { get; set; }
        public string permalink_url { get; set; }
        public string genre { get; set; }
        public string permalink { get; set; }
        public object purchase_url { get; set; }
        public object release_month { get; set; }
        public object description { get; set; }
        public string uri { get; set; }
        public object label_name { get; set; }
        public string tag_list { get; set; }
        public object release_year { get; set; }
        public int track_count { get; set; }
        public int user_id { get; set; }
        public string last_modified { get; set; }
        public string license { get; set; }
        public Track[] tracks { get; set; }
        public object playlist_type { get; set; }
        public int id { get; set; }
        public bool? downloadable { get; set; }
        public string sharing { get; set; }
        public string created_at { get; set; }
        public object release { get; set; }
        public string kind { get; set; }
        public string title { get; set; }
        public object type { get; set; }
        public object purchase_title { get; set; }
        public Created_With created_with { get; set; }
        public object artwork_url { get; set; }
        public object ean { get; set; }
        public bool? streamable { get; set; }
        public User user { get; set; }
        public string embeddable_by { get; set; }
        public object label_id { get; set; }
    }


    public class TrackCreatedWith
    {
        public int id { get; set; }
        public string kind { get; set; }
        public string name { get; set; }
        public string uri { get; set; }
        public string permalink_url { get; set; }
        public string external_url { get; set; }
    }

    public class Created_With
    {
        public string permalink_url { get; set; }
        public string name { get; set; }
        public string external_url { get; set; }
        public string uri { get; set; }
        public string creator { get; set; }
        public int id { get; set; }
        public string kind { get; set; }
    }


    public class User
    {
        public string permalink_url { get; set; }
        public string permalink { get; set; }
        public string username { get; set; }
        public string uri { get; set; }
        public string last_modified { get; set; }
        public int id { get; set; }
        public string kind { get; set; }
        public string avatar_url { get; set; }
    }

    public class Track
    {
        public string kind { get; set; }
        public int id { get; set; }
        public string created_at { get; set; }
        public int user_id { get; set; }
        public int duration { get; set; }
        public bool commentable { get; set; }
        public string state { get; set; }
        public int original_content_size { get; set; }
        public string last_modified { get; set; }
        public string sharing { get; set; }
        public string tag_list { get; set; }
        public string permalink { get; set; }
        public bool? streamable { get; set; }
        public string embeddable_by { get; set; }
        public bool downloadable { get; set; }
        public string purchase_url { get; set; }
        public int? label_id { get; set; }
        public string purchase_title { get; set; }
        public string genre { get; set; }
      
        private string _title = null;
        public string Title
        {
            get
            {
                return EffectiveDownloadUrl == download_url ? _title + "_High_Quality" : _title;
            }
            set
            {
                _title = Sanitize(value);
            }
        }
        public string EffectiveDownloadUrl
        {
            get
            {
                string url = !string.IsNullOrWhiteSpace(download_url) ?
                    download_url : stream_url;
                if (!string.IsNullOrWhiteSpace(url))
                {
                    return url.Replace("\r", "").Replace("\n", "");
                }
                else
                {
                    return null;
                }
            }
        }

        private string _path = null;
        public string LocalPath
        {
            get
            {
                return _path;
            }
            set
            {
                _path = value;
            }
        }

        public bool IsHD { get { return download_url == EffectiveDownloadUrl; } }

        public string Sanitize(string input)
        {
            Regex regex = new Regex(@"[^\w\s\d-]");
            return input != null ?
                regex.Replace(input.Replace("&amp;", "and")
                    .Replace("&", "and").Replace(".", "_"),
                   string.Empty)
                : null;
        }

        public string description { get; set; }
        public string label_name { get; set; }
        public string release { get; set; }
        public string track_type { get; set; }
        public string key_signature { get; set; }
        public string isrc { get; set; }
        public string video_url { get; set; }
        public float? bpm { get; set; }
        public int? release_year { get; set; }
        public int? release_month { get; set; }
        public int? release_day { get; set; }
        public string original_format { get; set; }
        public string license { get; set; }
        public string uri { get; set; }
        public User user { get; set; }
        public string permalink_url { get; set; }
        public string artwork_url { get; set; }
        public string waveform_url { get; set; }
        public string stream_url { get; set; }
        public int playback_count { get; set; }
        public int download_count { get; set; }
        public int favoritings_count { get; set; }
        public int comment_count { get; set; }
        public string attachments_uri { get; set; }
        public string policy { get; set; }
        public string download_url { get; set; }
        public Label label { get; set; }
        public string[] available_country_codes { get; set; }
        public TrackCreatedWith TrackCreatedWith { get; set; }

        public string Username
        {
            get
            {
                return user.username;
            }
            set
            {
                user.username = Sanitize(value);
            }
        }
    }

   

    public class Label
    {
        public int id { get; set; }
        public string kind { get; set; }
        public string permalink { get; set; }
        public string username { get; set; }
        public string last_modified { get; set; }
        public string uri { get; set; }
        public string permalink_url { get; set; }
        public string avatar_url { get; set; }
    }


}
