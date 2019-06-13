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
open iText.Layout

type Document = iText.Layout.Document

type CmdOptions =
    { [<Option('o', "out", Default = "./book.pdf", HelpText = "Location of the PDF file output")>]
      OutputPath : string
      [<Option("oid", Required = true, HelpText = "vk.com source wall's owner (user or group) id")>]
      OwnerId : Int64 }

let readImageBytes (url : Uri) =
    async {
        let req = WebRequest.CreateDefault(url)
        use! r = req.GetResponseAsync() |> Async.AwaitTask
        use ns = r.GetResponseStream()
        use ms = new MemoryStream()
        ns.CopyTo(ms, 81920)
        return ms.ToArray()
    }

let areaBreak (areaBreakType : AreaBreakType) =
    AreaBreak(new Nullable<AreaBreakType>(areaBreakType))

let vkPostToBookChapter (document : Document) (post : WallPost) =
    let paragraph = Paragraph()
    let ppp = System.Text.CodePagesEncodingProvider.Instance
    System.Text.Encoding.RegisterProvider(ppp)
    let fontFilePath = Environment.GetEnvironmentVariable("SystemRoot") + "\\fonts\\georgia.ttf"
    paragraph.SetTextAlignment(new Nullable<TextAlignment>(TextAlignment.JUSTIFIED))
             .SetFont(PdfFontFactory.CreateFont(fontFilePath, "CP1251", true)).Add(post.Text)
    |> ignore
    document.Add(paragraph) |> ignore
    async {
        let! attachmentImagesRaw = post.ImageAttachments
                                   |> Seq.map readImageBytes
                                   |> Async.Parallel
        let document =
            attachmentImagesRaw
            |> Seq.map (fun bytes -> Image(iText.IO.Image.ImageDataFactory.CreateJpeg(bytes)))
            |> Seq.map
                   (fun img ->
                   img.SetMargins(float32 10., float32 0., float32 10., float32 0.)
                      .SetAutoScaleWidth(true)
                      .SetHorizontalAlignment(Nullable<HorizontalAlignment>
                                                  (HorizontalAlignment.CENTER)))
            |> Seq.fold (fun (doc : Document) img -> doc.Add(img)) document
        document.Add(areaBreak AreaBreakType.NEXT_PAGE) |> ignore
        return ()
    }

let getConfig =
    let accessToken = Environment.GetEnvironmentVariable("VK_ACCESS_TOKEN")
    if String.IsNullOrEmpty(accessToken) then failwith "VK_ACCESS_TOKEN env var is required"
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
        let vkPostToBookChapter = vkPostToBookChapter document
        async {
            let! wall = getTransformedWallPosts api ownerId
            return! wall
                    |> Seq.rev
                    |> Seq.map vkPostToBookChapter
                    |> Async.Parallel
                    |> Async.Ignore
        }
        |> Async.RunSynchronously
        0
    | :? (NotParsed<CmdOptions>) -> failwith "Parsing of cmd arguments failed"
    | x ->
        sprintf "CommandLine.Parser.Default.ParseArguments returned unexpected result: %O" x
        |> failwith
