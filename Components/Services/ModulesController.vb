'
' DotNetNuke® - http://www.dotnetnuke.com
' Copyright (c) 2002-2011
' by DotNetNuke Corporation
'
' Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated 
' documentation files (the "Software"), to deal in the Software without restriction, including without limitation 
' the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and 
' to permit persons to whom the Software is furnished to do so, subject to the following conditions:
'
' The above copyright notice and this permission notice shall be included in all copies or substantial portions 
' of the Software.
'
' THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED 
' TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL 
' THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF 
' CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER 
' DEALINGS IN THE SOFTWARE.
'
Option Strict On
Option Explicit On

Imports DotNetNuke.Modules.Blog.Common
Imports System.Globalization
Imports System.Runtime.Serialization
Imports System.Runtime.Serialization.Json
Imports System.Net
Imports System.Net.Http
Imports System.Web.Http
Imports DotNetNuke.Web.Api
Imports DotNetNuke.Modules.Blog.Entities.Blogs
Imports DotNetNuke.Modules.Blog.Entities.Posts
Imports DotNetNuke.Modules.Blog.Security
Imports DotNetNuke.Modules.Blog.Integration
Imports DotNetNuke.Entities.Modules
Imports System.Linq
Imports System.Xml
Imports System.Net.Http.Formatting

Namespace Services

 Public Class ModulesController
  Inherits DnnApiController

  Public Class AddModuleDTO
   Public Property paneName As String
   Public Property position As Integer
   Public Property title As String
   Public Property template As String
  End Class

#Region " Private Members "
#End Region

#Region " Service Methods "
  <HttpPost()>
  <DnnModuleAuthorize(accesslevel:=DotNetNuke.Security.SecurityAccessLevel.Edit)>
  <ValidateAntiForgeryToken()>
  <ActionName("Add")>
  Public Function AddModule(postData As AddModuleDTO) As HttpResponseMessage

   Dim newSettings As New Common.ViewSettings(-1)
   newSettings.BlogModuleId = ActiveModule.ModuleID
   newSettings.Template = postData.template
   newSettings.ShowManagementPanel = False
   newSettings.ShowComments = False

   Dim objModule As New ModuleInfo
   objModule.Initialize(PortalSettings.PortalId)
   objModule.PortalID = PortalSettings.PortalId
   objModule.TabID = PortalSettings.ActiveTab.TabID
   objModule.ModuleOrder = postData.position
   If postData.title = "" Then
    objModule.ModuleTitle = "Blog"
   Else
    objModule.ModuleTitle = postData.title
   End If
   objModule.PaneName = postData.paneName
   objModule.ModuleDefID = ActiveModule.ModuleDefID
   objModule.InheritViewPermissions = True
   objModule.AllTabs = False
   objModule.Alignment = ""

   Dim objModules As New ModuleController
   objModule.ModuleID = objModules.AddModule(objModule)
   newSettings.UpdateSettings(objModule.TabModuleID)

   Return Request.CreateResponse(HttpStatusCode.OK, New With {.Result = "success"})

  End Function

  <HttpGet()>
  <AllowAnonymous()>
  <ActionName("APIRSD")>
  Public Function ApiRsd() As HttpResponseMessage

   Dim res As New HttpResponseMessage(HttpStatusCode.OK)
   Dim userId As Integer = -1
   HttpContext.Current.Request.Params.ReadValue("UserId", userId)

   Using out As New IO.StringWriter
    Using output As New XmlTextWriter(out)

     output.Formatting = Formatting.Indented
     output.WriteStartDocument()
     output.WriteStartElement("rsd")
     output.WriteAttributeString("version", "1.0")
     output.WriteStartElement("service")

     output.WriteElementString("engineName", "DNN Blog")
     output.WriteElementString("engineLink", "http://www.dnnblog.org")
     output.WriteElementString("homePageLink", DotNetNuke.Common.Globals.NavigateURL(ActiveModule.TabID))
     output.WriteStartElement("apis")
     output.WriteStartElement("api")
     output.WriteAttributeString("name", "MetaWeblog")
     output.WriteAttributeString("preferred", "true")
     output.WriteAttributeString("apiLink", DotNetNuke.Common.ResolveUrl(String.Format("~/DesktopModules/Blog/blogpost.ashx?portalid={0}&tabid={1}&moduleid={2}", PortalSettings.PortalId, ActiveModule.TabID, ActiveModule.ModuleID)))
     output.WriteAttributeString("blogID", "0")
     output.WriteEndElement() ' api
     output.WriteStartElement("api")
     output.WriteAttributeString("name", "MetaWeblog")
     output.WriteAttributeString("preferred", "false")
     output.WriteAttributeString("apiLink", DotNetNuke.Common.ResolveUrl(String.Format("~/DesktopModules/Blog/blogpost.ashx?portalid={0}&tabid={1}&moduleid={2}", PortalSettings.PortalId, ActiveModule.TabID, ActiveModule.ModuleID)))
     output.WriteAttributeString("blogID", "1")
     output.WriteEndElement() ' api
     output.WriteEndElement() ' apis
     output.WriteEndElement() ' service
     output.WriteEndElement() ' rsd
     output.Flush()

    End Using

    res.Content = New StringContent(out.ToString, System.Text.Encoding.UTF8, "application/xml")

   End Using

   Return res

  End Function

  <HttpGet()>
  <AllowAnonymous()>
  <ActionName("Manifest")>
  Public Function GetManifest() As HttpResponseMessage

   Dim res As New HttpResponseMessage(HttpStatusCode.OK)

   Using out As New IO.StringWriter
    Using output As New XmlTextWriter(out)
     Dim bs As ModuleSettings = ModuleSettings.GetModuleSettings(ActiveModule.ModuleID)

     output.Formatting = Formatting.Indented
     output.WriteStartDocument()
     output.WriteStartElement("manifest")
     output.WriteAttributeString("xmlns", "http://schemas.microsoft.com/wlw/manifest/weblog")
     output.WriteStartElement("options")

     output.WriteElementString("clientType", "Metaweblog")
     output.WriteElementString("supportsMultipleCategories", bs.AllowMultipleCategories.ToYesNo)
     output.WriteElementString("supportsCategories", CBool(bs.VocabularyId <> -1).ToYesNo)
     output.WriteElementString("supportsCustomDate", "Yes")
     output.WriteElementString("supportsKeywords", "Yes")
     output.WriteElementString("supportsTrackbacks", "No")
     output.WriteElementString("supportsEmbeds", "No")
     output.WriteElementString("supportsAuthor", "No")
     output.WriteElementString("supportsExcerpt", (CBool(bs.SummaryModel = Globals.SummaryType.PlainTextIndependent)).ToYesNo)
     output.WriteElementString("supportsPassword", "No")
     output.WriteElementString("supportsPages", "No")
     output.WriteElementString("supportsPageParent", "No")
     output.WriteElementString("supportsPageOrder", "No")
     output.WriteElementString("supportsEmptyTitles", "No")
     output.WriteElementString("supportsExtendedPosts", (CBool(bs.SummaryModel = Globals.SummaryType.HtmlPrecedesPost)).ToYesNo)
     output.WriteElementString("supportsCommentPolicy", "Yes")
     output.WriteElementString("supportsPingPolicy", "No")
     output.WriteElementString("supportsPostAsDraft", "Yes")
     output.WriteElementString("supportsFileUpload", "Yes")
     output.WriteElementString("supportsSlug", "No")
     output.WriteElementString("supportsHierarchicalCategories", "Yes")
     output.WriteElementString("supportsCategoriesInline", "Yes")
     output.WriteElementString("supportsNewCategories", "No")
     output.WriteElementString("supportsNewCategoriesInline", "No")
     output.WriteElementString("requiresXHTML", "Yes")

     output.WriteEndElement() ' options
     output.WriteEndElement() ' manifest
     output.Flush()

    End Using

    res.Content = New StringContent(out.ToString, System.Text.Encoding.UTF8, "application/xml")

   End Using

   Return res

  End Function
#End Region

#Region " Private Methods "
#End Region

 End Class

End Namespace