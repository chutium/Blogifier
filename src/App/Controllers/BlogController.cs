﻿using Core.Data;
using Core.Helpers;
using Core.Services;
using Markdig;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Xml;

namespace App.Controllers
{
    public class BlogController : Controller
    {
        IDataService _db;
        IFeedService _ss;
        SignInManager<AppUser> _sm;
        private readonly ICompositeViewEngine _viewEngine;
        static readonly string _listView = "~/Views/Themes/{0}/List.cshtml";

        public BlogController(IDataService db, IFeedService ss, SignInManager<AppUser> sm, ICompositeViewEngine viewEngine)
        {
            _db = db;
            _ss = ss;
            _sm = sm;
            _viewEngine = viewEngine;
        }

        public async Task<IActionResult> Index(int page = 1, string term = "")
        {
            ElastosAPI api = new ElastosAPI();
            string did1 = api.GetDID("C740869D015E674362B1F441E3EDBE1CBCF4FE8B709AA1A77E5CCA2C92BAF99D");
            string did2 = api.GetDID("B81F84282313A005087A3418F3FD903DD4162DDC63EDA38617FA8697419C377D");
            string info = api.GetDIDInfo("c544b21d49f387e34e0d975f2f92aec7faefaacb136a6cf7c85616f6b8c189a3", DIDInfoKey.announcement);
            /*string txid = api.SetDIDInfo("B81F84282313A005087A3418F3FD903DD4162DDC63EDA38617FA8697419C377D",
                DIDInfoKey.announcement,
                JObject.Parse(@"{
                    'date': '2018-11-04',
                    'text': 'Hello from Berlin Hackathon - Toni and Boran'
                }")
            );*/

            var blog = await _db.CustomFields.GetBlogSettings();
            var pager = new Pager(page, blog.ItemsPerPage);
            IEnumerable<PostItem> posts;

            if (string.IsNullOrEmpty(term))
            {
                posts = await _db.BlogPosts.GetList(p => p.Published > DateTime.MinValue, pager);
            }
            else
            {
                posts = await _db.BlogPosts.Search(pager, term);
            }

            if (pager.ShowOlder) pager.LinkToOlder = $"blog?page={pager.Older}";
            if (pager.ShowNewer) pager.LinkToNewer = $"blog?page={pager.Newer}";

            var model = new ListModel {
                Blog = blog,
                PostListType = PostListType.Blog,
                Posts = posts,
                Pager = pager
            };

            if (!string.IsNullOrEmpty(term))
            {
                model.Blog.Title = term;
                model.Blog.Description = "";
                model.PostListType = PostListType.Search;
            }

            return View(string.Format(_listView, blog.Theme), model);
        }

        [Route("posts/{slug}")]
        public async Task<IActionResult> Single(string slug)
        {
            try
            {
                var model = await _db.BlogPosts.GetModel(slug);
                model.Post.Content = Markdown.ToHtml(model.Post.Content);

                model.Blog = await _db.CustomFields.GetBlogSettings();

                model.Blog.Cover = string.IsNullOrEmpty(model.Post.Cover) ? 
                    $"{Url.Content("~/")}{model.Blog.Cover}" : 
                    $"{Url.Content("~/")}{model.Post.Cover}";
                model.Blog.Title = model.Post.Title;

                return View($"~/Views/Themes/{model.Blog.Theme}/Post.cshtml", model);
            }
            catch
            {
                return Redirect("~/error/404");
            }
            
        }

        [Route("authors/{name}")]
        public async Task<IActionResult> Authors(string name, int page = 1)
        {
            var blog = await _db.CustomFields.GetBlogSettings();
            var author = await _db.Authors.GetItem(a => a.AppUserName == name);

            var pager = new Pager(page, blog.ItemsPerPage);
            var posts = await _db.BlogPosts.GetList(p => p.Published > DateTime.MinValue && p.AuthorId == author.Id, pager);

            if (pager.ShowOlder) pager.LinkToOlder = $"authors/{name}?page={pager.Older}";
            if (pager.ShowNewer) pager.LinkToNewer = $"authors/{name}?page={pager.Newer}";

            var model = new ListModel {
                PostListType = PostListType.Author,
                Author = author,
                Posts = posts,
                Pager = pager
            };

            model.Blog = blog;
            model.Blog.Cover = $"{Url.Content("~/")}{model.Blog.Cover}";
            model.Blog.Description = "";

            return View(string.Format(_listView, model.Blog.Theme), model);
        }

        [Route("categories/{name}")]
        public async Task<IActionResult> Categories(string name, int page = 1)
        {
            var blog = await _db.CustomFields.GetBlogSettings();
            var pager = new Pager(page, blog.ItemsPerPage);
            var posts = await _db.BlogPosts.GetListByCategory(name, pager);

            if (pager.ShowOlder) pager.LinkToOlder = $"categories/{name}?page={pager.Older}";
            if (pager.ShowNewer) pager.LinkToNewer = $"categories/{name}?page={pager.Newer}";

            var model = new ListModel {
                PostListType = PostListType.Category,
                Posts = posts,
                Pager = pager
            };

            model.Blog = blog;
            model.Blog.Cover = $"{Url.Content("~/")}{model.Blog.Cover}";

            ViewBag.Category = name;
            model.Blog.Description = "";

            return View(string.Format(_listView, model.Blog.Theme), model);
        }

        [Route("feed/{type}")]
        public async Task Rss(string type)
        {
            Response.ContentType = "application/xml";
            string host = Request.Scheme + "://" + Request.Host;

            using (XmlWriter xmlWriter = XmlWriter.Create(Response.Body, new XmlWriterSettings() { Async = true, Indent = true }))
            {
                var posts = await _ss.GetEntries(type, host);

                if (posts != null && posts.Count() > 0)
                {
                    var lastUpdated = posts.FirstOrDefault().Published;
                    var writer = await _ss.GetWriter(type, host, xmlWriter);

                    foreach (var post in posts)
                    {
                        post.Description = Markdown.ToHtml(post.Description);
                        await writer.Write(post);
                    }
                }
            }
        }

        [Route("error/{code:int}")]
        public async Task<IActionResult> Error(int code)
        {
            var model = new PostModel();

            model.Blog = await _db.CustomFields.GetBlogSettings();
            model.Blog.Cover = $"{Url.Content("~/")}{model.Blog.Cover}";

            var viewName = $"~/Views/Themes/{model.Blog.Theme}/Error.cshtml";
            var result = _viewEngine.GetView("", viewName, false);

            if (result.Success)
            {
                return View(viewName, model);
            }
            else
            {
                return View("~/Views/Shared/_Error.cshtml", model);
            }
        }

        [HttpPost, Route("account/logout")]
        public async Task<IActionResult> Logout()
        {
            await _sm.SignOutAsync();
            return Redirect("~/");
        }
    }
}