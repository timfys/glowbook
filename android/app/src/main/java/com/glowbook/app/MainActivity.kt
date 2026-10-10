package com.glowbook.app

import android.Manifest
import android.annotation.SuppressLint
import android.app.Activity
import android.app.Notification
import android.app.NotificationChannel
import android.app.NotificationManager
import android.app.PendingIntent
import android.content.ClipData
import android.content.ClipboardManager
import android.content.Context
import android.content.Intent
import android.content.pm.PackageManager
import android.graphics.Color
import android.net.ConnectivityManager
import android.net.NetworkCapabilities
import android.net.Uri
import android.os.Build
import android.os.Bundle
import android.provider.ContactsContract
import android.view.Gravity
import android.view.View
import android.view.WindowManager
import android.webkit.JavascriptInterface
import android.webkit.ValueCallback
import android.webkit.WebChromeClient
import android.webkit.WebResourceError
import android.webkit.WebResourceRequest
import android.webkit.WebResourceResponse
import android.webkit.WebSettings
import android.webkit.WebView
import android.webkit.WebViewClient
import android.widget.Button
import android.widget.FrameLayout
import android.widget.LinearLayout
import android.widget.TextView
import org.json.JSONObject

class MainActivity : Activity() {

    private lateinit var webView: WebView
    private lateinit var errorPanel: LinearLayout
    private lateinit var loadingPanel: LinearLayout
    private lateinit var errorText: TextView
    private lateinit var mediaGallery: MediaGallery
    private var filePathCallback: ValueCallback<Array<Uri>>? = null
    private var pendingContactPick = false
    private var retryCount = 0
    private var lastFailedUrl: String = BASE_URL

    @SuppressLint("SetJavaScriptEnabled")
    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        window.setSoftInputMode(WindowManager.LayoutParams.SOFT_INPUT_ADJUST_RESIZE)
        mediaGallery = MediaGallery(this)
        ensureNotificationChannel()

        val root = FrameLayout(this)
        root.setBackgroundColor(Color.parseColor("#F6F3F8"))
        webView = WebView(this)
        webView.setBackgroundColor(Color.parseColor("#F6F3F8"))
        root.addView(
            webView,
            FrameLayout.LayoutParams(
                FrameLayout.LayoutParams.MATCH_PARENT,
                FrameLayout.LayoutParams.MATCH_PARENT
            )
        )

        errorPanel = LinearLayout(this).apply {
            orientation = LinearLayout.VERTICAL
            gravity = Gravity.CENTER
            setBackgroundColor(Color.parseColor("#FFF8F6FC"))
            setPadding(48, 48, 48, 48)
            visibility = View.GONE
        }
        errorText = TextView(this).apply {
            textSize = 16f
            setTextColor(Color.parseColor("#3D3550"))
            gravity = Gravity.CENTER
        }
        val retryButton = Button(this).apply {
            text = getString(R.string.retry_load)
            setOnClickListener { reloadFromError() }
        }
        loadingPanel = LinearLayout(this).apply {
            orientation = LinearLayout.VERTICAL
            gravity = Gravity.CENTER
            setBackgroundColor(Color.parseColor("#F6F3F8"))
            setPadding(48, 48, 48, 48)
        }
        loadingPanel.addView(TextView(this).apply {
            text = getString(R.string.loading_app)
            textSize = 16f
            setTextColor(Color.parseColor("#3D3550"))
            gravity = Gravity.CENTER
        })
        root.addView(
            loadingPanel,
            FrameLayout.LayoutParams(
                FrameLayout.LayoutParams.MATCH_PARENT,
                FrameLayout.LayoutParams.MATCH_PARENT
            )
        )

        errorPanel.addView(errorText)
        errorPanel.addView(retryButton, LinearLayout.LayoutParams(
            LinearLayout.LayoutParams.WRAP_CONTENT,
            LinearLayout.LayoutParams.WRAP_CONTENT
        ).apply { topMargin = 32 })
        root.addView(
            errorPanel,
            FrameLayout.LayoutParams(
                FrameLayout.LayoutParams.MATCH_PARENT,
                FrameLayout.LayoutParams.MATCH_PARENT
            )
        )
        setContentView(root)

        val settings = webView.settings
        settings.javaScriptEnabled = true
        settings.domStorageEnabled = true
        settings.databaseEnabled = true
        settings.allowFileAccess = true
        settings.allowContentAccess = true
        settings.loadsImagesAutomatically = true
        settings.cacheMode = WebSettings.LOAD_DEFAULT
        settings.useWideViewPort = true
        settings.loadWithOverviewMode = true
        settings.mixedContentMode = WebSettings.MIXED_CONTENT_COMPATIBILITY_MODE

        webView.addJavascriptInterface(AndroidBridge(), "GlowBookAndroid")

        webView.webViewClient = object : WebViewClient() {
            override fun shouldInterceptRequest(
                view: WebView?,
                request: WebResourceRequest?
            ): WebResourceResponse? {
                val uri = request?.url
                if (uri != null) {
                    mediaGallery.intercept(uri)?.let { return it }
                }
                return super.shouldInterceptRequest(view, request)
            }

            override fun onPageFinished(view: WebView?, url: String?) {
                retryCount = 0
                hideError()
                hideLoading()
            }

            override fun onReceivedError(
                view: WebView?,
                request: WebResourceRequest?,
                error: WebResourceError?
            ) {
                if (request?.isForMainFrame != true) return
                handleLoadFailure(request.url?.toString() ?: BASE_URL, error?.errorCode ?: WebViewClient.ERROR_UNKNOWN)
            }

            @Deprecated("Deprecated in Java")
            override fun onReceivedError(
                view: WebView?,
                errorCode: Int,
                description: String?,
                failingUrl: String?
            ) {
                handleLoadFailure(failingUrl ?: BASE_URL, errorCode)
            }
        }

        webView.webChromeClient = object : WebChromeClient() {
            override fun onShowFileChooser(
                webView: WebView?,
                filePathCallback: ValueCallback<Array<Uri>>?,
                fileChooserParams: FileChooserParams?
            ): Boolean {
                this@MainActivity.filePathCallback?.onReceiveValue(null)
                this@MainActivity.filePathCallback = filePathCallback

                val intent = try {
                    fileChooserParams?.createIntent()
                } catch (_: Exception) {
                    null
                } ?: Intent(Intent.ACTION_GET_CONTENT).apply {
                    addCategory(Intent.CATEGORY_OPENABLE)
                    type = "image/*"
                }

                return try {
                    @Suppress("DEPRECATION")
                    startActivityForResult(
                        Intent.createChooser(intent, "Выберите фото"),
                        REQUEST_FILE_CHOOSER
                    )
                    true
                } catch (_: Exception) {
                    this@MainActivity.filePathCallback?.onReceiveValue(null)
                    this@MainActivity.filePathCallback = null
                    false
                }
            }
        }

        if (savedInstanceState != null) {
            webView.restoreState(savedInstanceState)
            resolveOpenUrl(intent)?.let { loadAppUrl(it) }
        } else {
            loadAppUrl(resolveOpenUrl(intent) ?: BASE_URL)
        }
    }

    override fun onNewIntent(intent: Intent?) {
        super.onNewIntent(intent)
        setIntent(intent)
        resolveOpenUrl(intent)?.let { loadAppUrl(it) }
    }

    private fun resolveOpenUrl(intent: Intent?): String? {
        val fromNotify = intent?.getStringExtra(EXTRA_OPEN_URL)?.trim().orEmpty()
        if (fromNotify.isNotEmpty()) return absolutizeUrl(fromNotify)
        return resolveDeepLink(intent)
    }

    private fun absolutizeUrl(url: String): String {
        if (url.startsWith("http://", ignoreCase = true) || url.startsWith("https://", ignoreCase = true)) {
            return url
        }
        return if (url.startsWith("/")) "$BASE_URL$url" else "$BASE_URL/$url"
    }

    private fun ensureNotificationChannel() {
        if (Build.VERSION.SDK_INT < Build.VERSION_CODES.O) return
        val mgr = getSystemService(NotificationManager::class.java) ?: return
        val channel = NotificationChannel(
            NOTIFY_CHANNEL_ID,
            "GlowBox",
            NotificationManager.IMPORTANCE_HIGH
        ).apply {
            description = "Сообщения и записи"
            enableVibration(true)
        }
        mgr.createNotificationChannel(channel)
    }

    private fun postLocalNotification(title: String, body: String, url: String) {
        if (Build.VERSION.SDK_INT >= 33
            && checkSelfPermission(Manifest.permission.POST_NOTIFICATIONS) != PackageManager.PERMISSION_GRANTED
        ) {
            return
        }

        val absolute = absolutizeUrl(url)
        val tapIntent = Intent(this, MainActivity::class.java).apply {
            flags = Intent.FLAG_ACTIVITY_CLEAR_TOP or Intent.FLAG_ACTIVITY_SINGLE_TOP
            putExtra(EXTRA_OPEN_URL, absolute)
            data = Uri.parse(absolute)
        }
        val flags = PendingIntent.FLAG_UPDATE_CURRENT or PendingIntent.FLAG_IMMUTABLE
        val pending = PendingIntent.getActivity(this, absolute.hashCode(), tapIntent, flags)

        val builder = if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O) {
            Notification.Builder(this, NOTIFY_CHANNEL_ID)
        } else {
            @Suppress("DEPRECATION")
            Notification.Builder(this)
        }

        val notification = builder
            .setContentTitle(title.ifBlank { "GlowBox" })
            .setContentText(body)
            .setStyle(Notification.BigTextStyle().bigText(body))
            .setSmallIcon(R.mipmap.ic_launcher)
            .setContentIntent(pending)
            .setAutoCancel(true)
            .setPriority(Notification.PRIORITY_HIGH)
            .build()

        val mgr = getSystemService(NotificationManager::class.java) ?: return
        mgr.notify((System.currentTimeMillis() % Int.MAX_VALUE).toInt(), notification)
    }

    override fun onSaveInstanceState(outState: Bundle) {
        super.onSaveInstanceState(outState)
        webView.saveState(outState)
    }

    private fun resolveDeepLink(intent: Intent?): String? {
        val uri = intent?.data ?: return null
        val path = uri.path.orEmpty()
        return when {
            uri.scheme == "https"
                && uri.host.equals(APP_HOST, ignoreCase = true)
                && (path.startsWith("/u") || path.startsWith("/book")) -> uri.toString()

            uri.scheme.equals("glowbox", ignoreCase = true)
                && uri.host.equals("u", ignoreCase = true) -> {
                val username = uri.path?.trim('/')?.trim().orEmpty()
                if (username.isBlank()) BASE_URL
                else "$BASE_URL/u/$username"
            }

            uri.scheme.equals("glowbox", ignoreCase = true)
                && uri.host.equals("book", ignoreCase = true) -> {
                val slug = uri.path?.trim('/')?.substringBefore('/')?.trim().orEmpty()
                if (slug.isBlank()) "$BASE_URL/book"
                else "$BASE_URL/book/$slug"
            }

            else -> null
        }
    }

    private fun loadAppUrl(url: String) {
        lastFailedUrl = url
        hideError()
        showLoading()
        webView.loadUrl(url)
    }

    private fun reloadFromError() {
        retryCount = 0
        loadAppUrl(lastFailedUrl.ifBlank { BASE_URL })
    }

    private fun handleLoadFailure(url: String, errorCode: Int) {
        lastFailedUrl = url.ifBlank { BASE_URL }

        if (retryCount < MAX_AUTO_RETRIES && isRetryableError(errorCode)) {
            retryCount++
            val delayMs = 1200L * retryCount
            webView.postDelayed({ loadAppUrl(lastFailedUrl) }, delayMs)
            return
        }

        showError(errorCode)
    }

    private fun isRetryableError(errorCode: Int): Boolean {
        return errorCode == WebViewClient.ERROR_TIMEOUT
            || errorCode == WebViewClient.ERROR_HOST_LOOKUP
            || errorCode == WebViewClient.ERROR_CONNECT
            || errorCode == WebViewClient.ERROR_IO
            || errorCode == WebViewClient.ERROR_FAILED_SSL_HANDSHAKE
    }

    private fun showError(errorCode: Int) {
        val online = isNetworkAvailable()
        errorText.text = when {
            !online -> getString(R.string.error_offline)
            errorCode == WebViewClient.ERROR_TIMEOUT -> getString(R.string.error_timeout)
            else -> getString(R.string.error_generic)
        }
        hideLoading()
        errorPanel.visibility = View.VISIBLE
    }

    private fun hideError() {
        errorPanel.visibility = View.GONE
    }

    private fun showLoading() {
        loadingPanel.visibility = View.VISIBLE
    }

    private fun hideLoading() {
        loadingPanel.visibility = View.GONE
    }

    private fun isNetworkAvailable(): Boolean {
        val cm = getSystemService(Context.CONNECTIVITY_SERVICE) as? ConnectivityManager ?: return true
        val network = cm.activeNetwork ?: return false
        val caps = cm.getNetworkCapabilities(network) ?: return false
        return caps.hasCapability(NetworkCapabilities.NET_CAPABILITY_INTERNET)
    }

    private fun hasMediaPermission(): Boolean {
        return if (Build.VERSION.SDK_INT >= 33) {
            checkSelfPermission(Manifest.permission.READ_MEDIA_IMAGES) == PackageManager.PERMISSION_GRANTED &&
                checkSelfPermission(Manifest.permission.READ_MEDIA_VIDEO) == PackageManager.PERMISSION_GRANTED
        } else {
            checkSelfPermission(Manifest.permission.READ_EXTERNAL_STORAGE) == PackageManager.PERMISSION_GRANTED
        }
    }

    private fun mediaPermissions(): Array<String> {
        return if (Build.VERSION.SDK_INT >= 33) {
            arrayOf(
                Manifest.permission.READ_MEDIA_IMAGES,
                Manifest.permission.READ_MEDIA_VIDEO
            )
        } else {
            arrayOf(Manifest.permission.READ_EXTERNAL_STORAGE)
        }
    }

    inner class AndroidBridge {
        @JavascriptInterface
        fun pickContact() {
            runOnUiThread {
                if (checkSelfPermission(Manifest.permission.READ_CONTACTS) != PackageManager.PERMISSION_GRANTED) {
                    pendingContactPick = true
                    @Suppress("DEPRECATION")
                    requestPermissions(
                        arrayOf(Manifest.permission.READ_CONTACTS),
                        REQUEST_READ_CONTACTS
                    )
                    return@runOnUiThread
                }
                launchContactPicker()
            }
        }

        /** Reliable clipboard for WebView (navigator.clipboard is flaky there). */
        @JavascriptInterface
        fun copyText(text: String?): Boolean {
            if (text.isNullOrEmpty()) return false
            return try {
                val clipboard = getSystemService(Context.CLIPBOARD_SERVICE) as ClipboardManager
                clipboard.setPrimaryClip(ClipData.newPlainText("GlowBox", text))
                true
            } catch (_: Exception) {
                false
            }
        }

        @JavascriptInterface
        fun hasNativeGallery(): Boolean = true

        @JavascriptInterface
        fun hasGalleryPermission(): Boolean = hasMediaPermission()

        @JavascriptInterface
        fun requestGalleryPermission() {
            runOnUiThread {
                if (hasMediaPermission()) {
                    notifyGalleryPermission(true)
                    return@runOnUiThread
                }
                @Suppress("DEPRECATION")
                requestPermissions(mediaPermissions(), REQUEST_READ_MEDIA)
            }
        }

        @JavascriptInterface
        fun listGallery(offset: Int, limit: Int): String {
            if (!hasMediaPermission()) {
                return JSONObject()
                    .put("error", "permission")
                    .put("items", org.json.JSONArray())
                    .put("hasMore", false)
                    .toString()
            }
            return try {
                mediaGallery.listMedia(offset, limit)
            } catch (_: Exception) {
                JSONObject()
                    .put("error", "query")
                    .put("items", org.json.JSONArray())
                    .put("hasMore", false)
                    .toString()
            }
        }

        @JavascriptInterface
        fun listDocuments(offset: Int, limit: Int): String {
            if (!hasMediaPermission()) {
                return JSONObject()
                    .put("error", "permission")
                    .put("items", org.json.JSONArray())
                    .put("hasMore", false)
                    .toString()
            }
            return try {
                mediaGallery.listDocuments(offset, limit)
            } catch (_: Exception) {
                JSONObject()
                    .put("error", "query")
                    .put("items", org.json.JSONArray())
                    .put("hasMore", false)
                    .toString()
            }
        }

        @JavascriptInterface
        fun requestNotificationPermission() {
            runOnUiThread {
                if (Build.VERSION.SDK_INT >= 33
                    && checkSelfPermission(Manifest.permission.POST_NOTIFICATIONS)
                    != PackageManager.PERMISSION_GRANTED
                ) {
                    @Suppress("DEPRECATION")
                    requestPermissions(
                        arrayOf(Manifest.permission.POST_NOTIFICATIONS),
                        REQUEST_POST_NOTIFICATIONS
                    )
                }
            }
        }

        @JavascriptInterface
        fun showNotification(title: String?, body: String?, url: String?) {
            val t = title?.trim().orEmpty().ifBlank { "GlowBox" }
            val b = body?.trim().orEmpty().ifBlank { "Новое уведомление" }
            val u = url?.trim().orEmpty().ifBlank { BASE_URL }
            runOnUiThread {
                postLocalNotification(t, b, u)
            }
        }
    }

    private fun launchContactPicker() {
        val intent = Intent(Intent.ACTION_PICK, ContactsContract.Contacts.CONTENT_URI)
        try {
            @Suppress("DEPRECATION")
            startActivityForResult(intent, REQUEST_CONTACT_PICK)
        } catch (_: Exception) {
            notifyContactError()
        }
    }

    private fun notifyGalleryPermission(granted: Boolean) {
        webView.post {
            webView.evaluateJavascript(
                "window.GlowBook&&window.GlowBook.onGalleryPermission&&window.GlowBook.onGalleryPermission(${if (granted) "true" else "false"})",
                null
            )
        }
    }

    override fun onRequestPermissionsResult(
        requestCode: Int,
        permissions: Array<out String>,
        grantResults: IntArray
    ) {
        super.onRequestPermissionsResult(requestCode, permissions, grantResults)
        when (requestCode) {
            REQUEST_READ_CONTACTS -> {
                if (grantResults.isNotEmpty() && grantResults[0] == PackageManager.PERMISSION_GRANTED) {
                    if (pendingContactPick) {
                        pendingContactPick = false
                        launchContactPicker()
                    }
                } else {
                    pendingContactPick = false
                    notifyContactError()
                }
            }

            REQUEST_READ_MEDIA -> {
                val granted = grantResults.isNotEmpty() &&
                    grantResults.all { it == PackageManager.PERMISSION_GRANTED }
                // Partial grant (Android 14+) still lets MediaStore return selected items
                val anyGranted = grantResults.any { it == PackageManager.PERMISSION_GRANTED }
                notifyGalleryPermission(granted || anyGranted || hasMediaPermission())
            }

            REQUEST_POST_NOTIFICATIONS -> {
                // No-op: next showNotification will work if granted
            }
        }
    }

    @Deprecated("Deprecated in Java")
    override fun onActivityResult(requestCode: Int, resultCode: Int, data: Intent?) {
        @Suppress("DEPRECATION")
        super.onActivityResult(requestCode, resultCode, data)

        when (requestCode) {
            REQUEST_FILE_CHOOSER -> {
                val result = if (resultCode == RESULT_OK) {
                    WebChromeClient.FileChooserParams.parseResult(resultCode, data)
                } else {
                    null
                }
                filePathCallback?.onReceiveValue(result)
                filePathCallback = null
            }

            REQUEST_CONTACT_PICK -> {
                if (resultCode != RESULT_OK || data?.data == null) {
                    return
                }
                deliverContact(data.data!!)
            }
        }
    }

    private fun deliverContact(contactUri: Uri) {
        var name = ""
        var phone = ""

        contentResolver.query(
            contactUri,
            arrayOf(ContactsContract.Contacts._ID, ContactsContract.Contacts.DISPLAY_NAME),
            null,
            null,
            null
        )?.use { cursor ->
            if (cursor.moveToFirst()) {
                name = cursor.getString(
                    cursor.getColumnIndexOrThrow(ContactsContract.Contacts.DISPLAY_NAME)
                ) ?: ""
                val id = cursor.getString(
                    cursor.getColumnIndexOrThrow(ContactsContract.Contacts._ID)
                )
                phone = queryPrimaryPhone(id)
            }
        }

        if (name.isBlank() && phone.isBlank()) {
            notifyContactError()
            return
        }

        val payload = JSONObject()
            .put("name", name)
            .put("phone", phone)
            .toString()

        webView.post {
            webView.evaluateJavascript(
                "window.GlowBook&&window.GlowBook.onContactSelected($payload)",
                null
            )
        }
    }

    private fun queryPrimaryPhone(contactId: String): String {
        val phoneUri = ContactsContract.CommonDataKinds.Phone.CONTENT_URI
        val projection = arrayOf(ContactsContract.CommonDataKinds.Phone.NUMBER)
        val selection = "${ContactsContract.CommonDataKinds.Phone.CONTACT_ID}=?"
        contentResolver.query(phoneUri, projection, selection, arrayOf(contactId), null)?.use { cursor ->
            if (cursor.moveToFirst()) {
                return cursor.getString(
                    cursor.getColumnIndexOrThrow(ContactsContract.CommonDataKinds.Phone.NUMBER)
                ) ?: ""
            }
        }
        return ""
    }

    private fun notifyContactError() {
        webView.post {
            webView.evaluateJavascript(
                "window.GlowBook&&window.GlowBook.onContactPickFailed&&window.GlowBook.onContactPickFailed()",
                null
            )
        }
    }

    @Deprecated("Deprecated in Java")
    override fun onBackPressed() {
        if (errorPanel.visibility == View.VISIBLE) {
            reloadFromError()
            return
        }
        if (webView.canGoBack()) {
            webView.goBack()
        } else {
            @Suppress("DEPRECATION")
            super.onBackPressed()
        }
    }

    companion object {
        private const val APP_HOST = "glowbook-production-5e1a.up.railway.app"
        private const val BASE_URL = "https://$APP_HOST"
        private const val MAX_AUTO_RETRIES = 3
        private const val REQUEST_FILE_CHOOSER = 1001
        private const val REQUEST_CONTACT_PICK = 1002
        private const val REQUEST_READ_CONTACTS = 1003
        private const val REQUEST_READ_MEDIA = 1004
        private const val REQUEST_POST_NOTIFICATIONS = 1005
        private const val NOTIFY_CHANNEL_ID = "glowbox_alerts"
        private const val EXTRA_OPEN_URL = "open_url"
    }
}
