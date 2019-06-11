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
open CommandLine

type Document = iText.Layout.Document

type CmdOptions =
    { [<Option('o', "out", Default = "./book.pdf", HelpText = "Location of the PDF file output")>]
      OutputPath : string
      [<Option("oid", Required = true, HelpText = "vk.com source wall's owner (user or group) id")>]
      OwnerId : Int64 }

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

//TODO: refactor it, especially NotParsed case
[<EntryPoint>]
let main argv =
    let accessToken = getConfig
    let result = CommandLine.Parser.Default.ParseArguments<CmdOptions>(argv)
    match result with
    | :? (Parsed<CmdOptions>) as parsed ->
        let ownerId = parsed.Value.OwnerId |> OwnerId
        let outFilePath = Path.GetFullPath(parsed.Value.OutputPath)
        use api = new VkApi()
        do api.Authorize(ApiAuthParams(AccessToken = accessToken))
        use fs = new FileStream(outFilePath, FileMode.Create)
        use writer = new PdfWriter(fs)
        use pdf = new PdfDocument(writer)
        use document = new Document(pdf)
        async {
            let! wall = getTransformedWallPosts api ownerId
            wall
            |> Seq.rev
            |> Seq.iter (vkPostToBookChapter document)
        }
        |> Async.RunSynchronously
        0
    | :? (NotParsed<CmdOptions>) ->
        failwith "Parsing of cmd arguments failed"
    | x ->
        sprintf "CommandLine.Parser.Default.ParseArguments returned unexpected result: %O" x
        |> failwith
