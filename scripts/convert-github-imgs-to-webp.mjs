import webp from "webp-converter"
import {readdirSync, unlinkSync} from "fs"
import { dirname, join } from "path"
import { fileURLToPath } from "url";


const __filename = fileURLToPath(import.meta.url);
const __dirname = dirname(__filename);
const docs_github_path = join(__dirname,"../",".github")

const files = readdirSync(docs_github_path)
// const files = join(__dirname,"../",".github")

/** @type Promise<any>[] */
var convertions = []

/** @type string[] */
var files_path = []

files.forEach(file_name => {

    if(!file_name.includes(".png"))
        return

    const input_image_path = join(docs_github_path,file_name)
    const output_image_path = join(docs_github_path,`${file_name.replace(".png","")}.webp`)
    
    files_path.push(input_image_path)
    convertions.push(webp.cwebp(input_image_path,output_image_path))
})

Promise.all(convertions).then(_ => {
    console.log("Images converted: ",convertions.length);

    files_path.forEach(path => {
        unlinkSync(path)
    })
})
