@ECHO OFF
SETLOCAL
SET DIRNAME=%~dp0
IF "%DIRNAME%" == "" SET DIRNAME=.
SET APP_HOME=%DIRNAME%
SET APP_BASENAME=Gradle
SET APP_NAME=Gradle

SET DEFAULT_JVM_OPTS="-Xmx64m" "-Xms64m"

SET JAVA_EXE=java.exe
IF DEFINED JAVA_HOME (
  SET JAVA_EXE=%JAVA_HOME%\bin\java.exe
)

SET CLASSPATH=%APP_HOME%\gradle\wrapper\gradle-wrapper.jar

"%JAVA_EXE%" %DEFAULT_JVM_OPTS% %JAVA_OPTS% %GRADLE_OPTS% "-Dorg.gradle.appname=%APP_BASENAME%" -classpath "%CLASSPATH%" org.gradle.wrapper.GradleWrapperMain %*
