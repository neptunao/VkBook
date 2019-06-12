module VkBook.Domain

open System

type WallPost =
    { Text : string
      ImageAttachments : Uri [] }

let (|SafeInt64|) (v) =
    if v = 0L then ArgumentException("Id type can't be zero") |> raise
    v
