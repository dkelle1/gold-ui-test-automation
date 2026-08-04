from selenium.webdriver.remote.webdriver import WebDriver


def capture_screenshot_png(driver: WebDriver) -> bytes:
    return driver.get_screenshot_as_png()
