float SelectTextureLayer(float row)
{
    if(row < 0.1) {
        return 0.0;
    }
    else if(row < 0.2) {
        return 1.0;
    }
    else if(row < 0.3) {
        return 2.0;
    }
    else if(row < 0.4) {
        return 3.0;
    }
    else if(row < 0.5) {
        return 4.0;
    }
    else if(row < 0.6) {
        return 5.0;
    }
    else if(row < 0.7) {
        return 6.0;
    }
    else if(row < 0.8) {
        return 7.0;
    }
    else if(row < 0.9) {
        return 8.0;
    }
    else {
        return 9.0;
    }
}