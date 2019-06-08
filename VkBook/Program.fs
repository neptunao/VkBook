open System
open VkNet
open VkNet.Model
open System.IO
open System.Net
open iText.Layout.Element
open iText.Layout.Properties
open iText.Kernel.Pdf
open iText.Kernel.Font
open VkBook.Vk
open VkBook.Domain

type Document = iText.Layout.Document

// Image size 604 x 339
let vkPostToBookChapter (document : Document) (post : WallPost) =
    let paragraph = new Paragraph()
    // paragraph.SetSpacingBefore <- float32 10
    // paragraph.SpacingAfter <- float32 10
    paragraph.SetTextAlignment(new Nullable<TextAlignment>(TextAlignment.JUSTIFIED))
    let ppp = System.Text.CodePagesEncodingProvider.Instance
    System.Text.Encoding.RegisterProvider(ppp)
    let fontFilePath = Environment.GetEnvironmentVariable("SystemRoot") + "\\fonts\\georgia.ttf"
    paragraph.SetFont(PdfFontFactory.CreateFont(fontFilePath, "CP1251", true))
    paragraph.Add(post.Text)
    document.Add(paragraph)
    post.ImageAttachments
    |> Seq.iter (fun atm ->
           let req = WebRequest.CreateDefault(atm)
           use r = req.GetResponse()
           use ns = r.GetResponseStream()
           use ms = new MemoryStream()
           ns.CopyTo(ms, 81920)
           let img = new Image(iText.IO.Image.ImageDataFactory.CreateJpeg(ms.ToArray()))
           img.SetMargins(float32 10., float32 0., float32 10., float32 0.)
           img.ScaleToFit(img.GetImageWidth(), float32 339)
           //    img.SetAutoScaleWidth(true)
           img.SetHorizontalAlignment(Nullable<HorizontalAlignment>(HorizontalAlignment.CENTER))
           document.Add(img) |> ignore)
    document.Add(new AreaBreak(new Nullable<AreaBreakType>(AreaBreakType.NEXT_PAGE))) |> ignore
    ()

let getConfig =
    let accessToken = Environment.GetEnvironmentVariable("ACCESS_TOKEN")
    if String.IsNullOrEmpty(accessToken) then failwith "ACCESS_TOKEN env var is required"
    accessToken

[<EntryPoint>]
let main argv =
    let ownerId = argv.[0] |> int64
    let accessToken = getConfig
    use api = new VkApi()
    do api.Authorize(ApiAuthParams(AccessToken = accessToken))
    use fs = new FileStream("book.pdf", FileMode.Create)
    use writer = new PdfWriter(fs)
    use pdf = new PdfDocument(writer)
    use document = new Document(pdf)
    let wall = getTransformedWallPosts api ownerId
    wall
    |> Seq.rev
    |> Seq.iter (vkPostToBookChapter document)
    0
