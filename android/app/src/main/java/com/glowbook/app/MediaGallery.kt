package com.glowbook.app

import android.content.ContentResolver
import android.content.ContentUris
import android.content.Context
import android.graphics.Bitmap
import android.net.Uri
import android.os.Build
import android.os.Bundle
import android.provider.MediaStore
import android.util.Size
import android.webkit.WebResourceResponse
import org.json.JSONArray
import org.json.JSONObject
import java.io.ByteArrayInputStream
import java.io.ByteArrayOutputStream
import java.util.concurrent.ConcurrentHashMap

/**
 * In-app MediaStore gallery for WebView (Telegram-style): list + serve thumbs/full bytes
 * via https://glowbox.media/... intercepted in WebViewClient.
 */
class MediaGallery(private val context: Context) {

    data class Entry(
        val id: String,
        val uri: Uri,
        val mime: String,
        val name: String,
        val size: Long,
        val kind: String // image | video | file
    )

    private val entries = ConcurrentHashMap<String, Entry>()

    fun listMedia(offset: Int, limit: Int): String {
        val safeOffset = offset.coerceAtLeast(0)
        val safeLimit = limit.coerceIn(1, 120)
        val need = safeOffset + safeLimit
        val collected = queryMedia(need)

        val page = collected.drop(safeOffset).take(safeLimit)
        page.forEach { entries[it.id] = it }

        val arr = JSONArray()
        page.forEach { e ->
            arr.put(
                JSONObject()
                    .put("id", e.id)
                    .put("kind", e.kind)
                    .put("mime", e.mime)
                    .put("name", e.name)
                    .put("size", e.size)
                    .put("thumbUrl", "$HOST/thumb/${e.id}")
                    .put("itemUrl", "$HOST/item/${e.id}")
            )
        }

        return JSONObject()
            .put("items", arr)
            .put("hasMore", collected.size > safeOffset + page.size)
            .toString()
    }

    fun listDocuments(offset: Int, limit: Int): String {
        val safeOffset = offset.coerceAtLeast(0)
        val safeLimit = limit.coerceIn(1, 80)
        val need = safeOffset + safeLimit
        val collected = queryPdfs(need)

        val page = collected.drop(safeOffset).take(safeLimit)
        page.forEach { entries[it.id] = it }

        val arr = JSONArray()
        page.forEach { e ->
            arr.put(
                JSONObject()
                    .put("id", e.id)
                    .put("kind", e.kind)
                    .put("mime", e.mime)
                    .put("name", e.name)
                    .put("size", e.size)
                    .put("itemUrl", "$HOST/item/${e.id}")
            )
        }

        return JSONObject()
            .put("items", arr)
            .put("hasMore", collected.size > safeOffset + page.size)
            .toString()
    }

    fun intercept(uri: Uri): WebResourceResponse? {
        if (!uri.host.equals(MEDIA_HOST, ignoreCase = true)) return null
        val segments = uri.pathSegments
        if (segments.size < 2) return notFound()
        val action = segments[0]
        val id = segments[1]
        val entry = entries[id] ?: return notFound()

        return when (action) {
            "thumb" -> serveThumb(entry)
            "item" -> serveItem(entry)
            else -> notFound()
        }
    }

    private fun serveThumb(entry: Entry): WebResourceResponse {
        return try {
            if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.Q) {
                val bitmap: Bitmap =
                    context.contentResolver.loadThumbnail(entry.uri, Size(320, 320), null)
                val bos = ByteArrayOutputStream()
                bitmap.compress(Bitmap.CompressFormat.JPEG, 82, bos)
                val bytes = bos.toByteArray()
                WebResourceResponse(
                    "image/jpeg",
                    null,
                    200,
                    "OK",
                    cacheHeaders(),
                    ByteArrayInputStream(bytes)
                )
            } else if (entry.kind == "image") {
                serveItem(entry)
            } else {
                notFound()
            }
        } catch (_: Exception) {
            notFound()
        }
    }

    private fun serveItem(entry: Entry): WebResourceResponse {
        return try {
            val stream = context.contentResolver.openInputStream(entry.uri) ?: return notFound()
            val mime = entry.mime.ifBlank { "application/octet-stream" }
            WebResourceResponse(
                mime,
                null,
                200,
                "OK",
                cacheHeaders(),
                stream
            )
        } catch (_: Exception) {
            notFound()
        }
    }

    private fun queryMedia(max: Int): List<Entry> {
        val collection = if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.Q) {
            MediaStore.Files.getContentUri(MediaStore.VOLUME_EXTERNAL)
        } else {
            MediaStore.Files.getContentUri("external")
        }
        val projection = arrayOf(
            MediaStore.Files.FileColumns._ID,
            MediaStore.Files.FileColumns.DISPLAY_NAME,
            MediaStore.Files.FileColumns.MIME_TYPE,
            MediaStore.Files.FileColumns.SIZE,
            MediaStore.Files.FileColumns.MEDIA_TYPE,
            MediaStore.Files.FileColumns.DATE_ADDED
        )
        val selection =
            "${MediaStore.Files.FileColumns.MEDIA_TYPE}=? OR ${MediaStore.Files.FileColumns.MEDIA_TYPE}=?"
        val selectionArgs = arrayOf(
            MediaStore.Files.FileColumns.MEDIA_TYPE_IMAGE.toString(),
            MediaStore.Files.FileColumns.MEDIA_TYPE_VIDEO.toString()
        )
        val out = ArrayList<Entry>(max)
        runQuery(
            collection,
            projection,
            selection,
            selectionArgs,
            MediaStore.Files.FileColumns.DATE_ADDED,
            max
        ) { cursor ->
            val id = cursor.getLong(0)
            val name = cursor.getString(1) ?: ""
            val mime = cursor.getString(2) ?: ""
            val size = cursor.getLong(3)
            val mediaType = cursor.getInt(4)
            val kind = if (mediaType == MediaStore.Files.FileColumns.MEDIA_TYPE_VIDEO) "video" else "image"
            val prefix = if (kind == "video") "vid" else "img"
            val contentUri = if (kind == "video") {
                ContentUris.withAppendedId(
                    if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.Q)
                        MediaStore.Video.Media.getContentUri(MediaStore.VOLUME_EXTERNAL)
                    else MediaStore.Video.Media.EXTERNAL_CONTENT_URI,
                    id
                )
            } else {
                ContentUris.withAppendedId(
                    if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.Q)
                        MediaStore.Images.Media.getContentUri(MediaStore.VOLUME_EXTERNAL)
                    else MediaStore.Images.Media.EXTERNAL_CONTENT_URI,
                    id
                )
            }
            out.add(
                Entry(
                    id = "${prefix}_$id",
                    uri = contentUri,
                    mime = mime.ifBlank { if (kind == "video") "video/mp4" else "image/jpeg" },
                    name = name.ifBlank { if (kind == "video") "video_$id.mp4" else "image_$id.jpg" },
                    size = size,
                    kind = kind
                )
            )
        }
        return out
    }

    private fun queryPdfs(max: Int): List<Entry> {
        val collection = if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.Q) {
            MediaStore.Files.getContentUri(MediaStore.VOLUME_EXTERNAL)
        } else {
            MediaStore.Files.getContentUri("external")
        }
        val projection = arrayOf(
            MediaStore.Files.FileColumns._ID,
            MediaStore.Files.FileColumns.DISPLAY_NAME,
            MediaStore.Files.FileColumns.MIME_TYPE,
            MediaStore.Files.FileColumns.SIZE
        )
        val selection =
            "${MediaStore.Files.FileColumns.MIME_TYPE}=? OR ${MediaStore.Files.FileColumns.DISPLAY_NAME} LIKE ?"
        val selectionArgs = arrayOf("application/pdf", "%.pdf")
        val out = ArrayList<Entry>(max)
        runQuery(
            collection,
            projection,
            selection,
            selectionArgs,
            MediaStore.Files.FileColumns.DATE_ADDED,
            max
        ) { cursor ->
            val id = cursor.getLong(0)
            val name = cursor.getString(1) ?: ""
            val mime = cursor.getString(2) ?: "application/pdf"
            val size = cursor.getLong(3)
            val uri = ContentUris.withAppendedId(collection, id)
            out.add(
                Entry(
                    id = "doc_$id",
                    uri = uri,
                    mime = mime.ifBlank { "application/pdf" },
                    name = name.ifBlank { "document_$id.pdf" },
                    size = size,
                    kind = "file"
                )
            )
        }
        return out
    }

    private fun runQuery(
        collection: Uri,
        projection: Array<String>,
        selection: String?,
        selectionArgs: Array<String>?,
        sortColumn: String,
        max: Int,
        onRow: (android.database.Cursor) -> Unit
    ) {
        try {
            val resolver = context.contentResolver
            if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O) {
                val args = Bundle().apply {
                    putInt(ContentResolver.QUERY_ARG_LIMIT, max)
                    putStringArray(ContentResolver.QUERY_ARG_SORT_COLUMNS, arrayOf(sortColumn))
                    putInt(
                        ContentResolver.QUERY_ARG_SORT_DIRECTION,
                        ContentResolver.QUERY_SORT_DIRECTION_DESCENDING
                    )
                    if (selection != null) {
                        putString(ContentResolver.QUERY_ARG_SQL_SELECTION, selection)
                        putStringArray(ContentResolver.QUERY_ARG_SQL_SELECTION_ARGS, selectionArgs)
                    }
                }
                resolver.query(collection, projection, args, null)?.use { cursor ->
                    var n = 0
                    while (cursor.moveToNext() && n < max) {
                        onRow(cursor)
                        n++
                    }
                }
            } else {
                resolver.query(
                    collection,
                    projection,
                    selection,
                    selectionArgs,
                    "$sortColumn DESC LIMIT $max"
                )?.use { cursor ->
                    var n = 0
                    while (cursor.moveToNext() && n < max) {
                        onRow(cursor)
                        n++
                    }
                }
            }
        } catch (_: Exception) {
            // Permission / provider failure — empty list
        }
    }

    private fun cacheHeaders(): Map<String, String> = mapOf(
        "Access-Control-Allow-Origin" to "*",
        "Cache-Control" to "private, max-age=300"
    )

    private fun notFound(): WebResourceResponse =
        WebResourceResponse(
            "text/plain",
            "utf-8",
            404,
            "Not Found",
            emptyMap(),
            ByteArrayInputStream(ByteArray(0))
        )

    companion object {
        const val MEDIA_HOST = "glowbox.media"
        const val HOST = "https://$MEDIA_HOST"
    }
}
