// Learn more about F# at http://fsharp.org
open System
open VkNet
open VkNet.Model
open VkNet.Model.RequestParams
open VkNet.Enums.SafetyEnums
open VkNet.Model.Attachments

type WallPost =
    { Text : string
      ImageAttachments : Uri [] }

// TODO: extract groupd id
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
    // getAllWallPostsRec 0 wall.TotalCount []
    getAllWallPostsRec 0 10uL []

let getTransformedWallPosts (api : VkApi) =
    getAllWallPostsRaw api 100uL
    |> Seq.map (fun a ->
           { Text = a.Text
             ImageAttachments =
                 a.Attachments
                 |> Seq.filter (fun atm -> atm.Type = typeof<Photo>)
                 |> Seq.map (fun atm -> atm.Instance :?> Photo)
                 // TODO: choose biggest one?
                 |> Seq.map (fun atm -> atm.Sizes.[0].Url)
                 |> Seq.toArray })

[<EntryPoint>]
let main argv =
    let api = new VkApi()
    do api.Authorize
           (ApiAuthParams
                (AccessToken = "0c937c853dcce5e28c6f64b25d0420d2dbb7eb8c493c848d4e46845302df4bc0afed0678fa840fc19415d"))
    let wall = getTransformedWallPosts api
    printf "%A" wall
    //6527732d123fe4156590f3b84c1c1645dfb3ad984951f4c23dfdcb9d68c3bface5a90932dd4353c95358f
    0 // return an integer exit code
