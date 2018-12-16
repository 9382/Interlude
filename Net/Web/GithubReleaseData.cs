﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace YAVSRG.Net.Web
{
    public class GithubReleaseData
    {
        public string url;
        public string tag_name;
        public string name;
        public string published_at;
        public string body;
        public List<GithubAsset> assets;
    }

    public class GithubAsset
    {
        public string name;
        public string browser_download_url;
    }
}
