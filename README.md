# LargeFileFiller
Creates a file and fills it with content until the file reaches a specified size.

It is basically the `fsutil file createnew` command but with more features.

## Usage
```
LargeFileFiller --HELP

LargeFileFiller FileName
    [--SIZE=integer]
    [--UNIT=B|KB|MB|GB]
    [--CONTENT=string]
    [--FILL=Null|Random|Fixed]
    [--APPEND]
    [--VERBOSE]
    [--NOBANNER]
    [--SILENT]
```

## Arguments
| Argument | Description | Type | Default |
| -------- |-------------| ---- | ------- |
| FileName | The file to write the output to. | string | |
| --SIZE | The output file size. | integer | 1 |
| --UNIT | The unit of measure for the file size. | B, KB, MB, GB | GB |
| --CONTENT | The string to use for filling the contents. | string | See `Notes` section below |
| --FILL | The order on which the contents are written to the output file. | Null, Random, Fixed | Null |
| --APPEND | Append the contents to the file if it exists already. | | |
| --VERBOSE | Display more information on the progress. | | |
| --NOBANNER | Hide the application banner. | | |
| --SILENT  | Terminate immediately after completion. | | |
| --HELP | Show the help message. | | |

## Notes
* If `--FILL` argument is `Null`, the contents written to the output file will consist of the null character (`\0`). The `--CONTENT` argument is ignored.

* If `--FILL` argument is `Random`, the contents written to the output file is a random selection of characters taken from the string specified on the `--CONTENT` argument.

  If the `--CONTENT` argument is not specified, the characters will be taken randomly from the following set:
  | Types | Values |
  | ----- | ------ |
  | Letters | `ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz` |
  | Numbers | `1234567890` |
  | Symbols | <code>!@#$%^&*()-=_+`~[]\\{}\|;':",./<>?</code> |
  | Non-printable characters | `<carriage return><line feed><tab><space>` |

* If `--FILL` argument is `Fixed`, the contents written to the output file is the exact string specified on the `--CONTENT` argument, repeated until the file reaches the specified size.

  If the `--CONTENT` argument is not specified, the string used will be the following:

  `Lorem ipsum dolor sit amet, consectetur adipiscing elit, sed do eiusmod tempor incididunt ut labore et dolore magna aliqua.<line feed>`

* You can press `<ESC>` key at anytime to cancel the operation.

  No new files will be generated if the operation is cancelled.

  If the output file already exists, it will not be overwritten or appended to if the operation is cancelled.