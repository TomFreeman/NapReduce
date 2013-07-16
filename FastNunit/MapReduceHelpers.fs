module MapReduceHelpers

let toB64Encoded text = System.Text.Encoding.UTF8.GetBytes(text.ToString())
                        |> fun bytes -> System.Convert.ToBase64String(bytes)

let fromB64Encoded text = System.Convert.FromBase64String(text)
                          |> System.Text.Encoding.UTF8.GetString