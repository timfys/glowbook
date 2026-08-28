package com.glowbook.app

import android.Manifest
import android.annotation.SuppressLint
import android.app.Activity
import android.content.Context
import android.content.Intent
import android.content.pm.PackageManager
import android.graphics.Color
import android.net.ConnectivityManager
import android.net.NetworkCapabilities
import android.net.Uri
import android.os.Bundle
import android.provider.ContactsContract
import android.view.Gravity
import android.view.View
import android.webkit.JavascriptInterface
import android.webkit.ValueCallback
import android.webkit.WebChromeClient
import android.webkit.WebResourceError
import android.webkit.WebResourceRequest
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
    private lateinit var errorText: TextView
    private var filePathCallback: ValueCallback<Array<Uri>>? = null
    private var pendingContactPick = false
    private var retryCount = 0
    private var lastFailedUrl: String = BASE_URL

    @SuppressLint("SetJavaScriptEnabled")
    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)

        val root = FrameLayout(this)
        webView = WebView(this)
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

        webView.addJavascriptInterface(ContactBridge(), "GlowBookAndroid")

        webView.webViewClient = object : WebViewClient() {
            override fun onPageFinished(view: WebView?, url: String?) {
                retryCount = 0
                hideError()
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
        } else {
            loadAppUrl(BASE_URL)
        }
    }

    override fun onSaveInstanceState(outState: Bundle) {
        super.onSaveInstanceState(outState)
        webView.saveState(outState)
    }

    private fun loadAppUrl(url: String) {
        lastFailedUrl = url
        hideError()
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
        errorPanel.visibility = View.VISIBLE
    }

    private fun hideError() {
        errorPanel.visibility = View.GONE
    }

    private fun isNetworkAvailable(): Boolean {
        val cm = getSystemService(Context.CONNECTIVITY_SERVICE) as? ConnectivityManager ?: return true
        val network = cm.activeNetwork ?: return false
        val caps = cm.getNetworkCapabilities(network) ?: return false
        return caps.hasCapability(NetworkCapabilities.NET_CAPABILITY_INTERNET)
    }

    inner class ContactBridge {
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

    override fun onRequestPermissionsResult(
        requestCode: Int,
        permissions: Array<out String>,
        grantResults: IntArray
    ) {
        super.onRequestPermissionsResult(requestCode, permissions, grantResults)
        if (requestCode != REQUEST_READ_CONTACTS) return

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
        private const val BASE_URL = "https://glowbook-production-5e1a.up.railway.app"
        private const val MAX_AUTO_RETRIES = 3
        private const val REQUEST_FILE_CHOOSER = 1001
        private const val REQUEST_CONTACT_PICK = 1002
        private const val REQUEST_READ_CONTACTS = 1003
    }
}
