
using Microsoft.VisualBasic;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
//
// DNN Connect - http://dnn-connect.org
// Copyright (c) 2014
// by DNN Connect
//
// Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated 
// documentation files (the "Software"), to deal in the Software without restriction, including without limitation 
// the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and 
// to permit persons to whom the Software is furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all copies or substantial portions 
// of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED 
// TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL 
// THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF 
// CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER 
// DEALINGS IN THE SOFTWARE.
//

using DotNetNuke.Web.Client.ClientResourceManagement;
using DotNetNuke.Entities.Modules;
using DotNetNuke.Framework;
using DotNetNuke.Modules.Blog.Security;
using DotNetNuke.Modules.Blog.Common.Globals;
using DotNetNuke.Services.Tokens;
using DotNetNuke.Modules.Blog.Entities.Terms;

namespace Common
{

	public class BlogContextInfo : IPropertyAccess
	{

		#region " Private Members "
		private NameValueCollection RequestParams { get; set; }
		#endregion

		#region " Public Methods "

		public BlogContextInfo(HttpContext context, BlogModuleBase blogModule)
		{
			BlogModuleId = blogModule.ModuleId;

			// Initialize values from View Settings
			if (blogModule.ViewSettings.BlogModuleId != -1) {
				BlogModuleId = blogModule.ViewSettings.BlogModuleId;
				ParentModule = (new DotNetNuke.Entities.Modules.ModuleController()).GetModule(BlogModuleId);
			}
			BlogId = blogModule.ViewSettings.BlogId;
			Categories = blogModule.ViewSettings.Categories;
			AuthorId = blogModule.ViewSettings.AuthorId;

			Locale = Threading.Thread.CurrentThread.CurrentCulture.Name;
			if (context.Request.UrlReferrer != null)
				Referrer = context.Request.UrlReferrer.PathAndQuery;
			RequestParams = context.Request.Params;

			context.Request.Params.ReadValue("Blog", BlogId);
			context.Request.Params.ReadValue("Post", ContentItemId);
			context.Request.Params.ReadValue("Term", TermId);
			context.Request.Params.ReadValue("Categories", Categories);
			context.Request.Params.ReadValue("User", AuthorId);
			context.Request.Params.ReadValue("uid", AuthorId);
			context.Request.Params.ReadValue("UserId", AuthorId);
			context.Request.Params.ReadValue("Author", AuthorId);
			context.Request.Params.ReadValue("end", EndDate);
			context.Request.Params.ReadValue("search", SearchString);
			context.Request.Params.ReadValue("t", SearchTitle);
			context.Request.Params.ReadValue("c", SearchContents);
			context.Request.Params.ReadValue("u", SearchUnpublished);
			context.Request.Params.ReadValue("EntryId", LegacyEntryId);
			if (ContentItemId > -1)
				Post = Entities.Posts.PostsController.GetPost(ContentItemId, BlogModuleId, Locale);
			if (BlogId > -1 & Post != null && Post.BlogID != BlogId)
				Post = null;
			// double check in case someone is hacking to retrieve an Post from another blog
			if (BlogId == -1 & Post != null)
				BlogId = Post.BlogID;
			if (BlogId > -1)
				Blog = Entities.Blogs.BlogsController.GetBlog(BlogId, blogModule.UserInfo.UserID, Locale);
			if (BlogId > -1)
				BlogMapPath = GetBlogDirectoryMapPath(BlogId);
			if (!string.IsNullOrEmpty(BlogMapPath) && !IO.Directory.Exists(BlogMapPath))
				IO.Directory.CreateDirectory(BlogMapPath);
			if (ContentItemId > -1)
				PostMapPath = GetPostDirectoryMapPath(BlogId, ContentItemId);
			if (!string.IsNullOrEmpty(PostMapPath) && !IO.Directory.Exists(PostMapPath))
				IO.Directory.CreateDirectory(PostMapPath);
			if (TermId > -1)
				Term = Entities.Terms.TermsController.GetTerm(TermId, BlogModuleId, Locale);
			if (AuthorId > -1)
				Author = DotNetNuke.Entities.Users.UserController.GetUserById(blogModule.PortalId, AuthorId);
			if (context.Request.UserAgent != null) {
				WLWRequest = Convert.ToBoolean(context.Request.UserAgent.IndexOf("Windows Live Writer") > -1);
			}
			Security = new ContextSecurity(BlogModuleId, blogModule.TabId, Blog, blogModule.UserInfo);
			if (EndDate < Now.AddDays(-1)) {
				EndDate = EndDate.Date.AddDays(1).AddMinutes(-1);
				EndDateOrNow = EndDate;
			} else if (Security.CanAddPost) {
				EndDate = null;
			} else {
				EndDate = DateTime.Now.ToUniversalTime;
				// security measure to stop people prying into future posts
				EndDateOrNow = EndDate;
			}

			// security
			bool isStylePostRequest = false;
			if (Post != null && !(Post.Published | Security.CanEditThisPost(Post)) && !Security.IsEditor) {
				if (Post.Title.Contains("3bfe001a-32de-4114-a6b4-4005b770f6d7") & WLWRequest) {
					isStylePostRequest = true;
				} else {
					Post = null;
					ContentItemId = -1;
				}
			}
			if (Blog != null && !Blog.Published && !Security.IsOwner && !Security.UserIsAdmin && !isStylePostRequest) {
				Blog = null;
				BlogId = -1;
			}

			// set urls for use in module
			if (ParentModule == null) {
				ModuleUrls = new ModuleUrls(blogModule.TabId, BlogId, ContentItemId, TermId, AuthorId);
			} else {
				ModuleUrls = new ModuleUrls(blogModule.TabId, ParentModule.TabID, BlogId, ContentItemId, TermId, AuthorId);
			}
			IsMultiLingualSite = Convert.ToBoolean(DotNetNuke.Services.Localization.LocaleController.Instance.GetLocales(blogModule.PortalId).Count > 1);
			if (!blogModule.ViewSettings.ShowAllLocales) {
				ShowLocale = Locale;
			}
			if (Referrer.Contains("/ctl/") | Referrer.Contains("&ctl=")) {
				Referrer = DotNetNuke.Common.NavigateURL(blogModule.TabId);
				// just catch 99% of bad referrals to edit pages
			}

			UiTimeZone = blogModule.ModuleContext.PortalSettings.TimeZone;
			if (blogModule.UserInfo.Profile.PreferredTimeZone != null) {
				UiTimeZone = blogModule.UserInfo.Profile.PreferredTimeZone;
			}

		}

		public static BlogContextInfo GetBlogContext(ref HttpContext context, BlogModuleBase blogModule)
		{
			BlogContextInfo res = null;
			if (context.Items("BlogContext" + blogModule.TabModuleId.ToString) == null) {
				res = new BlogContextInfo(context, blogModule);
				context.Items("BlogContext" + blogModule.TabModuleId.ToString) = res;
			} else {
				res = (BlogContextInfo)context.Items("BlogContext" + blogModule.TabModuleId.ToString);
			}
			return res;
		}
		#endregion

		#region " Public Properties "
		public int BlogModuleId { get; set; }
		public DotNetNuke.Entities.Modules.ModuleInfo ParentModule { get; set; }
		public int BlogId { get; set; }
		public int ContentItemId { get; set; }
		public int TermId { get; set; }
		public string Categories { get; set; }
		public int AuthorId { get; set; }
		public System.DateTime EndDate { get; set; }
		public System.DateTime EndDateOrNow { get; set; }
		public Entities.Blogs.BlogInfo Blog { get; set; }
		public Entities.Posts.PostInfo Post { get; set; }
		public Entities.Terms.TermInfo Term { get; set; }
		public DotNetNuke.Entities.Users.UserInfo Author { get; set; }
		public string BlogMapPath { get; set; }
		public string PostMapPath { get; set; }
		public bool OutputAdditionalFiles { get; set; }
		public ModuleUrls ModuleUrls { get; set; }
		public string SearchString { get; set; }
		public bool SearchTitle { get; set; }
		public bool SearchContents { get; set; }
		public bool SearchUnpublished { get; set; }
		public bool IsMultiLingualSite { get; set; }
		public string ShowLocale { get; set; }
		public string Locale { get; set; }
		public string Referrer { get; set; }
		public bool WLWRequest { get; set; }
		public TimeZoneInfo UiTimeZone { get; set; }
		public ContextSecurity Security { get; set; }
		public int LegacyEntryId { get; set; }
		#endregion

		#region " IPropertyAccess Implementation "
		public string GetProperty(string strPropertyName, string strFormat, System.Globalization.CultureInfo formatProvider, DotNetNuke.Entities.Users.UserInfo AccessingUser, DotNetNuke.Services.Tokens.Scope AccessLevel, ref bool PropertyNotFound)
		{
			string OutputFormat = string.Empty;
			DotNetNuke.Entities.Portals.PortalSettings portalSettings = DotNetNuke.Entities.Portals.PortalController.GetCurrentPortalSettings();
			if (strFormat == string.Empty) {
				OutputFormat = "D";
			} else {
				OutputFormat = strFormat;
			}
			switch (strPropertyName.ToLower) {
				case "blogmoduleid":
					return (this.BlogModuleId.ToString(OutputFormat, formatProvider));
				case "blogid":
					return (this.BlogId.ToString(OutputFormat, formatProvider));
				case "Postid":
				case "contentitemid":
				case "postid":
				case "post":
					return (this.ContentItemId.ToString(OutputFormat, formatProvider));
				case "termid":
				case "term":
					return (this.TermId.ToString(OutputFormat, formatProvider));
				case "categories":
					return this.Categories;
				case "authorid":
				case "author":
					return (this.AuthorId.ToString(OutputFormat, formatProvider));
				case "enddate":
					return (this.EndDate.ToString(OutputFormat, formatProvider));
				case "enddateornow":
					return (this.EndDateOrNow.ToString(OutputFormat, formatProvider));
				case "blogselected":
					return Convert.ToBoolean(BlogId > -1).ToString();
				case "postselected":
					return Convert.ToBoolean(ContentItemId > -1).ToString();
				case "termselected":
					return Convert.ToBoolean(TermId > -1).ToString();
				case "authorselected":
					return Convert.ToBoolean(AuthorId > -1).ToString();
				case "ismultilingualsite":
					return IsMultiLingualSite.ToString();
				case "showlocale":
					return ShowLocale;
				case "locale":
					switch (strFormat.ToLower) {
						case "3":
							return Threading.Thread.CurrentThread.CurrentCulture.ThreeLetterISOLanguageName;
						case "ietf":
							return Threading.Thread.CurrentThread.CurrentCulture.IetfLanguageTag;
						case "displayname":
						case "display":
							return Threading.Thread.CurrentThread.CurrentCulture.DisplayName;
						case "englishname":
						case "english":
							return Threading.Thread.CurrentThread.CurrentCulture.EnglishName;
						case "nativename":
						case "native":
							return Threading.Thread.CurrentThread.CurrentCulture.NativeName;
						case "generic":
						case "2":
							return Threading.Thread.CurrentThread.CurrentCulture.TwoLetterISOLanguageName;
						default:
							return Locale;
					}
					break;
				case "searchstring":
					return SearchString;
				case "issearch":
					return Convert.ToBoolean(!string.IsNullOrEmpty(SearchString)).ToString();
				case "referrer":
					return Referrer;
				default:
					if (RequestParams(strPropertyName) != null) {
						return RequestParams(strPropertyName);
					} else {
						PropertyNotFound = true;
					}
					break;
			}
			return DotNetNuke.Common.Utilities.Null.NullString;
		}

		public DotNetNuke.Services.Tokens.CacheLevel Cacheability {
			get { return CacheLevel.fullyCacheable; }
		}
		#endregion

	}

}

//=======================================================
//Service provided by Telerik (www.telerik.com)
//Conversion powered by NRefactory.
//Twitter: @telerik
//Facebook: facebook.com/telerik
//=======================================================
