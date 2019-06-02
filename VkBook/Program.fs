﻿open System
open VkNet
open VkNet.Model
open VkNet.Model.RequestParams
open VkNet.Enums.SafetyEnums
open VkNet.Model.Attachments
open iTextSharp.text
open iTextSharp.text.pdf
open System.IO

type WallPost =
    { Text : string
      ImageAttachments : Uri [] }

// TODO: extract group id
let getAllWallPostsRaw (api : VkApi) batchSize =
    let rec getAllWallPostsRec offset (remains : uint64) (res : Attachments.Post list) =
        match remains with
        | 0uL -> res
        | remains ->
            let countToGet = Math.Min(batchSize, remains)
            let wall =
                api.Wall.Get
                    (WallGetParams
                         (OwnerId = Nullable(-73664556L), Filter = WallFilter.Owner,
                          Count = countToGet, Offset = uint64 offset), skipAuthorization = false)

            let appended =
                wall.WallPosts
                |> List.ofSeq
                |> List.append res
            getAllWallPostsRec (offset + wall.WallPosts.Count)
                (remains - uint64 wall.WallPosts.Count) appended

    let wall = api.Wall.Get(WallGetParams(OwnerId = Nullable(-73664556L), Count = uint64 0))
    // TODO: uncomment and replace with
    getAllWallPostsRec 0 wall.TotalCount []

// getAllWallPostsRec 0 10uL []
let vkPostToBookChapter (document : Document) post =
    let paragraph = new Paragraph()
    paragraph.SpacingBefore <- float32 10
    paragraph.SpacingAfter <- float32 10
    paragraph.Alignment <- Element.ALIGN_JUSTIFIED
    let ppp = System.Text.CodePagesEncodingProvider.Instance
    System.Text.Encoding.RegisterProvider(ppp)
    let sylfaenpath = Environment.GetEnvironmentVariable("SystemRoot") + "\\fonts\\times.ttf"
    let sylfaen = BaseFont.CreateFont(sylfaenpath, BaseFont.IDENTITY_H, BaseFont.EMBEDDED)
    let normal = new Font(sylfaen, float32 12, Font.NORMAL, BaseColor.BLACK)
    paragraph.Font <- normal
    paragraph.Add(post.Text)
    document.Add(paragraph)
    document.NewPage()
    ()

let getConfig =
    let accessToken = Environment.GetEnvironmentVariable("ACCESS_TOKEN")
    if String.IsNullOrEmpty(accessToken) then failwith "ACCESS_TOKEN env var is required"
    accessToken

let getTransformedWallPosts (api : VkApi) =
    getAllWallPostsRaw api 100uL
    |> Seq.map (fun a ->
           { Text = a.Text
             ImageAttachments =
                 a.Attachments
                 |> Seq.filter (fun atm -> atm.Type = typeof<Photo>)
                 |> Seq.map (fun atm -> atm.Instance :?> Photo)
                 // TODO: choose smallest one?
                 |> Seq.map (fun atm -> atm.Sizes.[0].Url)
                 |> Seq.toArray })

[<EntryPoint>]
let main argv =
    let accessToken = getConfig
    use api = new VkApi()
    do api.Authorize(ApiAuthParams(AccessToken = accessToken))
    use document = new Document(PageSize.A4)
    let wall = getTransformedWallPosts api
    use fs = new FileStream("book.pdf", FileMode.Create)
    let writer = PdfWriter.GetInstance(document, fs)
    document.Open()
    wall
    |> Seq.rev
    |> Seq.iter (vkPostToBookChapter document)
    document.Close()
    0 // return an integer exit code
