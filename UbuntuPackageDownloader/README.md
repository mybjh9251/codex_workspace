# Ubuntu Package Downloader

Windows Online PC에서 Ubuntu Offline PC의 Local-Repo에 전달할 `.deb` package와 dependency를 다운로드하는 WPF 프로그램이다.

## 주요 기능

- Ubuntu Version 및 Architecture 선택
- Kakao Mirror 또는 Ubuntu Official repository 선택
- `main universe`, `release updates security` metadata 조회
- Windows native dependency resolution
- Requested/Dependency 구분과 report 표시
- 실행 파일 하위 `Download` 폴더에 `.deb` 및 report 저장
- 기존 파일 skip 및 SHA256 checksum 생성

## 사용 방법

1. Ubuntu Version과 Architecture를 선택한다.
2. Package List에 package 이름을 한 줄에 하나씩 입력한다.
3. `Download`를 선택한다.
4. 실행 파일 하위 `Download` 폴더에서 결과를 확인한다.

Package Version을 지정하려면 다음 형식을 사용한다.

```text
curl=8.5.0-2ubuntu10.9
```

## 출력 파일

```text
Download/
  *.deb
  manifest.json
  download-report.csv
  checksums.sha256
  unresolved-dependencies.csv
```

Offline PC에는 필요한 `.deb` 파일을 전달한다. Offline PC의 Local-Repo 구성과 설치 검증은 이 프로그램의 범위에 포함되지 않는다.

## Runtime

GitHub Release의 `win-x64` package는 self-contained 단일 실행 파일로 배포한다. WSL과 `apt-get`은 필요하지 않다.
