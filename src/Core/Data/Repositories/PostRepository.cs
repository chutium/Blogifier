﻿using Core.Helpers;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Core.Data
{
    public interface IPostRepository : IRepository<BlogPost>
    {
        Task<IEnumerable<PostItem>> GetList(Expression<Func<BlogPost, bool>> predicate, Pager pager);
        Task<IEnumerable<PostItem>> GetListByCategory(string category, Pager pager);
        Task<IEnumerable<PostItem>> Search(Pager pager, string term, int author = 0);
        Task<PostItem> GetItem(Expression<Func<BlogPost, bool>> predicate);
        Task<PostModel> GetModel(string slug);
        Task<PostItem> SaveItem(PostItem item);
        Task SaveCover(int postId, string asset);
    }

    public class PostRepository : Repository<BlogPost>, IPostRepository
    {
        AppDbContext _db;

        public PostRepository(AppDbContext db) : base(db)
        {
            _db = db;
        }

        public async Task<IEnumerable<PostItem>> GetList(Expression<Func<BlogPost, bool>> predicate, Pager pager)
        {
            var skip = pager.CurrentPage * pager.ItemsPerPage - pager.ItemsPerPage;

            var drafts = _db.BlogPosts
                .Where(p => p.Published == DateTime.MinValue).Where(predicate)
                .OrderByDescending(p => p.Published).ToList();

            var pubs = _db.BlogPosts
                .Where(p => p.Published > DateTime.MinValue).Where(predicate)
                .OrderByDescending(p => p.IsFeatured)
                .ThenByDescending(p => p.Published).ToList();

            var items = drafts.Concat(pubs).ToList();
            pager.Configure(items.Count);

            var postPage = items.Skip(skip).Take(pager.ItemsPerPage).ToList();

            return await Task.FromResult(PostListToItems(postPage));
        }

        public async Task<IEnumerable<PostItem>> GetListByCategory(string category, Pager pager)
        {
            var skip = pager.CurrentPage * pager.ItemsPerPage - pager.ItemsPerPage;

            var posts = _db.BlogPosts
                .Where(p => p.Published > DateTime.MinValue)
                .OrderByDescending(p => p.Published).ToList();

            var items = new List<BlogPost>();

            foreach (var item in posts)
            {
                if (!string.IsNullOrEmpty(item.Categories))
                {
                    var cats = item.Categories.ToLower().Split(',');
                    if (cats.Contains(category.ToLower()))
                    {
                        items.Add(item);
                    }
                }
            }

            pager.Configure(items.Count);

            var postPage = items.Skip(skip).Take(pager.ItemsPerPage).ToList();

            return await Task.FromResult(PostListToItems(postPage));
        }

        // search always returns only published posts
        // for a search term and optional blog author
        public async Task<IEnumerable<PostItem>> Search(Pager pager, string term, int author = 0)
        {
            var skip = pager.CurrentPage * pager.ItemsPerPage - pager.ItemsPerPage;
            var results = new List<SearchResult>();
            var list = new List<PostItem>();

            IEnumerable<BlogPost> posts;
            if (author == 0)
                posts = _db.BlogPosts.Where(p => p.Published > DateTime.MinValue).ToList();
            else
                posts = _db.BlogPosts.Where(p => p.Published > DateTime.MinValue && p.AuthorId == author).ToList();

            foreach (var item in posts)
            {
                var rank = 0;
                var hits = 0;
                term = term.ToLower();

                if (item.Title.ToLower().Contains(term))
                {
                    hits = Regex.Matches(item.Title.ToLower(), term).Count;
                    rank += hits * 10;
                }
                if (item.Description.ToLower().Contains(term))
                {
                    hits = Regex.Matches(item.Description.ToLower(), term).Count;
                    rank += hits * 3;
                }
                if (item.Content.ToLower().Contains(term))
                {
                    rank += Regex.Matches(item.Content.ToLower(), term).Count;
                }

                if (rank > 0)
                {
                    results.Add(new SearchResult { Rank = rank, Item = PostToItem(item) });
                }
            }
            results = results.OrderByDescending(r => r.Rank).ToList();
            for (int i = 0; i < results.Count; i++)
            {
                list.Add(results[i].Item);
            }
            pager.Configure(list.Count);
            return await Task.Run(() => list.Skip(skip).Take(pager.ItemsPerPage).ToList());
        }

        public async Task<PostItem> GetItem(Expression<Func<BlogPost, bool>> predicate)
        {
            var post = _db.BlogPosts.Single(predicate);
            var item = PostToItem(post);

            item.Author.Avatar = string.IsNullOrEmpty(item.Author.Avatar) ? "lib/img/avatar.jpg" : item.Author.Avatar;

            return await Task.FromResult(item);
        }

        public async Task<PostModel> GetModel(string slug)
        {
            var model = new PostModel();

            var all = _db.BlogPosts
                .OrderByDescending(p => p.IsFeatured)
                .ThenByDescending(p => p.Published).ToList();

            if(all != null && all.Count > 0)
            {
                for (int i = 0; i < all.Count; i++)
                {
                    if(all[i].Slug == slug)
                    {
                        model.Post = PostToItem(all[i]);

                        if(i > 0 && all[i - 1].Published > DateTime.MinValue)
                        {
                            model.Newer = PostToItem(all[i - 1]);
                        }

                        if (i + 1 < all.Count && all[i + 1].Published > DateTime.MinValue)
                        {
                            model.Older = PostToItem(all[i + 1]);
                        }

                        break;
                    }
                }
            }

            return await Task.FromResult(model);
        }

        public async Task<PostItem> SaveItem(PostItem item)
        {
            BlogPost post;
            var field = _db.CustomFields.Where(f => f.AuthorId == 0 && f.Name == Constants.BlogCover).FirstOrDefault();
            var cover = field == null ? "" : field.Content;

            ElastosAPI api = new ElastosAPI();
            var json = JObject.Parse(@"{
                    'title': '',
                    'hash': '',
                    'date': ''
                }");
            json["title"] = item.Title;
            json["hash"] = ElastosAPI.CreateMD5(item.Content);
            json["date"] = item.Published;
            string txid = api.SetDIDInfo(_db.AspNetUsers.Single(a => a.UserName == item.Author.AppUserName).PrivateKey,
                DIDInfoKey.post,
                json
            );

            if (item.Id == 0)
            {
                post = new BlogPost
                {
                    Title = item.Title,
                    Slug = item.Slug,
                    Content = item.Content,
                    Hash = ElastosAPI.CreateMD5(item.Content),
                    TxID = txid,
                    Description = item.Description ?? item.Title,
                    Categories = item.Categories,
                    Cover = item.Cover ?? cover,
                    AuthorId = item.Author.Id,
                    IsFeatured = item.Featured,
                    Published = item.Published
                };
                _db.BlogPosts.Add(post);
                await _db.SaveChangesAsync();

                post = _db.BlogPosts.Single(p => p.Slug == post.Slug);
                item = PostToItem(post);
            }
            else
            {
                post = _db.BlogPosts.Single(p => p.Id == item.Id);

                post.Title = item.Title;
                post.Slug = item.Slug;
                post.Content = item.Content;
                post.Hash = ElastosAPI.CreateMD5(item.Content);
                post.TxID = txid;
                post.Description = item.Description ?? item.Title;
                post.Categories = item.Categories;
                post.AuthorId = item.Author.Id;
                post.Published = item.Published;
                post.IsFeatured = item.Featured;
                await _db.SaveChangesAsync();

                item.Slug = post.Slug;
                item.Hash = post.Hash;
                item.TxID = post.TxID;
            }
            return await Task.FromResult(item);
        }

        public async Task SaveCover(int postId, string asset)
        {
            var item = _db.BlogPosts.Single(p => p.Id == postId);
            item.Cover = asset;

            await _db.SaveChangesAsync();
        }

        PostItem PostToItem(BlogPost p)
        {
            var post = new PostItem
            {
                Id = p.Id,
                Slug = p.Slug,
                Title = p.Title,
                Description = p.Description,
                Content = p.Content,
                Hash = p.Hash,
                TxID = p.TxID,
                Categories = p.Categories,
                Cover = p.Cover,
                PostViews = p.PostViews,
                Rating = p.Rating,
                Published = p.Published,
                Featured = p.IsFeatured,
                Author = _db.Authors.Single(a => a.Id == p.AuthorId)
            };
            if(post.Author != null)
            {
                post.Author.Avatar = string.IsNullOrEmpty(post.Author.Avatar) ?
                    AppSettings.Avatar : post.Author.Avatar;
            }
            return post;
        }

        public List<PostItem> PostListToItems(List<BlogPost> posts)
        {
            return posts.Select(p => new PostItem
            {
                Id = p.Id,
                Slug = p.Slug,
                Title = p.Title,
                Description = p.Description,
                Content = p.Content,
                Hash = p.Hash,
                TxID = p.TxID,
                Categories = p.Categories,
                Cover = p.Cover,
                PostViews = p.PostViews,
                Rating = p.Rating,
                Published = p.Published,
                Featured = p.IsFeatured,
                Author = _db.Authors.Single(a => a.Id == p.AuthorId)
            }).Distinct().ToList();
        }
    }

    internal class SearchResult
    {
        public int Rank { get; set; }
        public PostItem Item { get; set; }
    }
}