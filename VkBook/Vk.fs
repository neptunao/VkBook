module VkBook.Vk

open System
open VkNet
open VkNet.Model.RequestParams
open VkNet.Enums.SafetyEnums
open VkNet.Model.Attachments
open VkBook.Domain
open VkNet.Model

let batchSize = 100uL

let private getWallByOwnerAsync (api : VkApi) (ownerId : int64) (count : uint64) (offset : uint64) =
    let p =
        WallGetParams
            (OwnerId = Nullable(ownerId), Filter = WallFilter.Owner, Count = count, Offset = offset)
    api.Wall.GetAsync(p) |> Async.AwaitTask

// by introducing getWall as parameter we get purity as a nice bonus
let getAllWallPostsRaw getWall batchSize =
    let rec getAllWallPostsRec offset (remains : uint64) (res : Post list) =
        async {
            match remains with
            | 0uL -> return res
            | remains ->
                let countToGet = Math.Min(batchSize, remains)
                let! (wall : WallGetObject) = getWall countToGet offset
                let appended =
                    wall.WallPosts
                    |> List.ofSeq
                    |> List.append res
                return! getAllWallPostsRec (offset + uint64 wall.WallPosts.Count)
                            (remains - uint64 wall.WallPosts.Count) appended
        }
    async { let! wall = getWall 0uL 0uL
            return! getAllWallPostsRec 0uL wall.TotalCount [] }

let private getMaxSizePhotoAttachment (photo : Photo) =
    photo.Sizes
    |> Seq.maxBy (fun s -> s.Height + s.Width)
    |> (fun s -> s.Url)

let private transformPost (post : Post) =
    { Text = post.Text
      ImageAttachments =
          post.Attachments
          |> Seq.filter (fun atm -> atm.Type = typeof<Photo>)
          |> Seq.map (fun atm -> atm.Instance :?> Photo)
          |> Seq.map getMaxSizePhotoAttachment
          |> Seq.toArray }

let getTransformedWallPosts (api : VkApi) ownerId =
    let getWall = getWallByOwnerAsync api ownerId
    async { let! posts = getAllWallPostsRaw getWall batchSize
            return posts |> Seq.map transformPost }
