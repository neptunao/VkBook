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

let copyBufSize = 81920

type CmdOptions =
    { [<Option('o', "out", Default = "./book.pdf", HelpText = "Location of the PDF file output")>]
      OutputPath : string
      [<Option("oid", Required = true, HelpText = "vk.com source wall's owner (user or group) id")>]
      OwnerId : Int64
      [<Option("font-path", Default = "./fonts/Crimson-Roman.ttf", HelpText = "Font file")>]
      fontPath : string
      [<Option('e', "encoding", Default = "UTF-8", HelpText = "PDF document encoding")>]
      encoding : string
      [<Option("font-size", Default = 14.5f, HelpText = "Font size in points")>]
      fontSize : float32 }

let readImageBytes (url : Uri) =
    async {
        let req = WebRequest.CreateDefault(url)
        use! r = req.GetResponseAsync() |> Async.AwaitTask
        use ns = r.GetResponseStream()
        use ms = new MemoryStream()
        ns.CopyTo(ms, copyBufSize)
        return ms.ToArray()
    }

let areaBreak (areaBreakType : AreaBreakType) =
    AreaBreak(new Nullable<AreaBreakType>(areaBreakType))

let downloadPostImages (post : WallPost) =
    async {
        let! attachmentImagesRaw = post.ImageAttachments
                                   |> Seq.map readImageBytes
                                   |> Async.Parallel
        return { Text = post.Text
                 ImageAttachmentsRawBytes = attachmentImagesRaw }
    }

let private createFont (encoding : string) (fontFilePath : string) =
    PdfFontFactory.CreateFont(fontFilePath, encoding, true)

let vkPostToBookChapter (document : Document) (post : WallPostDownloaded) =
    let configureImage (img : Image) =
        img.SetMargins(float32 10., float32 0., float32 10., float32 0.).SetAutoScaleWidth(true)
           .SetHorizontalAlignment(Nullable<HorizontalAlignment>(HorizontalAlignment.CENTER))
    let paragraph = Paragraph()
    let paragraph =
        paragraph.SetTextAlignment(new Nullable<TextAlignment>(TextAlignment.JUSTIFIED))
                 .Add(post.Text)
    let document = document.Add(paragraph)

    let document =
        post.ImageAttachmentsRawBytes
        |> Seq.map (iText.IO.Image.ImageDataFactory.CreateJpeg
                    >> Image
                    >> configureImage)
        |> Seq.fold (fun (doc : Document) img -> doc.Add(img)) document
    document.Add(areaBreak AreaBreakType.NEXT_PAGE) |> ignore
    document

let getConfig =
    let accessToken = Environment.GetEnvironmentVariable("VK_ACCESS_TOKEN")
    if String.IsNullOrEmpty(accessToken) then failwith "VK_ACCESS_TOKEN env var is required"
    accessToken

[<EntryPoint>]
let main argv =
    let accessToken = getConfig
    let result = CommandLine.Parser.Default.ParseArguments<CmdOptions>(argv)
    let ppp = System.Text.CodePagesEncodingProvider.Instance
    System.Text.Encoding.RegisterProvider(ppp)
    match result with
    | :? (Parsed<CmdOptions>) as parsed ->
        let font =
            parsed.Value.fontPath
            |> Path.GetFullPath
            |> createFont parsed.Value.encoding

        let ownerId = parsed.Value.OwnerId |> OwnerId
        let outFilePath = Path.GetFullPath(parsed.Value.OutputPath)
        use api = new VkApi()
        do api.Authorize(ApiAuthParams(AccessToken = accessToken))
        use fs = new FileStream(outFilePath, FileMode.Create)
        use writer = new PdfWriter(fs)
        use pdf = new PdfDocument(writer)
        use document = (new Document(pdf)).SetFontSize(parsed.Value.fontSize).SetFont(font)
        async {
            let! wall = getTransformedWallPosts api ownerId
            let! wallDownloaded = wall
                                  |> Seq.rev
                                  |> Seq.map downloadPostImages
                                  |> Async.Parallel
            return wallDownloaded |> Seq.fold vkPostToBookChapter document
        }
        |> Async.RunSynchronously
        |> ignore
        0
    | :? (NotParsed<CmdOptions>) -> failwith "Parsing of cmd arguments failed"
    | x ->
        sprintf "CommandLine.Parser.Default.ParseArguments returned unexpected result: %O" x
        |> failwith
