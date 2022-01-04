# TwitterBookmarkDownload

Janky downloader for Twitter bookmarks. 
This app works by scrolling through the Twitter bookmarks timeline, capturing the graphql responses and downloading all media, tagging it with the poster screenname, tweet ID and media index.
Downloads are deduplicated, media isn't redownloaded if it already exists.

## Disclaimer

Twitter is falling apart. Sometimes, it forgets that you're logged in. Sometimes, it indicates that your timeline ended earlier than it actually does. Sometimes, tweets are missing from the timeline. Sometimes, the server just errors out.
There is no nice way to deal with this, so if you notice that this tool scrolled to the end of your timeline, I recommend running it again, to make sure that it really got everything.

The [feedback post](https://twitterdevfeedback.uservoice.com/forums/930250-twitter-api/suggestions/39678766-api-endpoint-for-getting-bookmarks) requesting a public API for bookmarks has been sitting idle since August 2020, when the feature was introduced. Considering the state of their public API, it probably won't happen.

If scrolling stops and shows a "retry" button, you might have to restart.

## Requirements
1. dotnet 6
2. `yt-dlp` in your path, if you have tweets with videos or gifs in your timeline
3. The contents of your `Cookie` header in a text file called `cookies.txt`. Path to this can also be specified with `--cookie-file`

Output folder defaults to `out`, can be specified with `--output-path`.

If you run into media types that aren't handled, or something doesn't work, please let me know.
