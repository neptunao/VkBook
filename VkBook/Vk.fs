module VkBook.Vk

open System
open VkNet
open VkNet.Model.RequestParams
open VkNet.Enums.SafetyEnums
open VkNet.Model.Attachments
open VkBook.Domain

let getAllWallPostsRaw (api : VkApi) ownerId batchSize =
    let rec getAllWallPostsRec offset (remains : uint64) (res : Post list) =
        match remains with
        | 0uL -> res
        | remains ->
            let countToGet = Math.Min(batchSize, remains)
            let wall =
                api.Wall.Get
                    (WallGetParams
                         (OwnerId = Nullable(ownerId), Filter = WallFilter.Owner, Count = countToGet,
                          Offset = uint64 offset))

            let appended =
                wall.WallPosts
                |> List.ofSeq
                |> List.append res
            getAllWallPostsRec (offset + wall.WallPosts.Count)
                (remains - uint64 wall.WallPosts.Count) appended

    let wall = api.Wall.Get(WallGetParams(OwnerId = Nullable(ownerId), Count = uint64 0))
    getAllWallPostsRec 0 wall.TotalCount []

let private getMaxSizePhotoAttachment (photo : Photo) =
    photo.Sizes
    |> Seq.maxBy (fun s -> s.Height + s.Width)
    |> (fun s -> s.Url)

let getTransformedWallPosts (api : VkApi) ownerId =
    let batchSize = 100uL
    getAllWallPostsRaw api ownerId batchSize
    |> Seq.map (fun a ->
           { Text = a.Text
             ImageAttachments =
                 a.Attachments
                 |> Seq.filter (fun atm -> atm.Type = typeof<Photo>)
                 |> Seq.map (fun atm -> atm.Instance :?> Photo)
                 |> Seq.map getMaxSizePhotoAttachment
                 |> Seq.toArray })
